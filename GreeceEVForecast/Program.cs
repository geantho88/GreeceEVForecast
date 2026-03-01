using GreeceEVForecast;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.ML;
using System.Diagnostics;
using System.Globalization;

namespace GreeceEvForecast
{
    class Program
    {
        private static string? connectionString;
        static void Main(string[] args)
        {
            Console.WriteLine("EV Infrastructure Prediction Starting...");

            var config = new ConfigurationBuilder()
              .SetBasePath(Directory.GetCurrentDirectory())
              .AddJsonFile("appsettings.json", optional: false)
              .Build();

            connectionString = config.GetConnectionString("ThesisDatabase");

            // Call the ML workflow
            RunEVCDIPrediction();

            Console.WriteLine("Prediction Completed.");
            Console.ReadLine();
        }

        /// <summary>
        /// Loads data, trains regression model, and predicts CDI for all grid cells
        /// </summary>
        static void RunEVCDIPrediction()
        {
            // 1. Create ML context
            var mlContext = new MLContext(seed: 0);

            string sqlQuery = @"SELECT GridX, GridY, HotelDensity, EVChargers, GasStations, InfrastructureGap, CDI FROM GridAggregatedView;";

            var gridData = new List<GridData>();
            var hotels = new List<HotelPoint>();
            var gasStations = new List<GasStationPoint>();
            var evStations = new List<EvStationPoint>();

            using (var sqlConn = new SqlConnection(connectionString))
            {
                sqlConn.Open();
                var command = new SqlCommand(sqlQuery, sqlConn);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        gridData.Add(new GridData
                        {
                            GridX = Convert.ToInt32(reader["GridX"]),
                            GridY = Convert.ToInt32(reader["GridY"]),
                            HotelDensity = Convert.ToSingle(reader["HotelDensity"]),
                            EVChargers = Convert.ToSingle(reader["EVChargers"]),
                            GasStations = Convert.ToSingle(reader["GasStations"]),
                            InfrastructureGap = Convert.ToSingle(reader["InfrastructureGap"]),
                            CDI = Convert.ToSingle(reader["CDI"])
                        });
                    }
                }



                var hotelCmd = new SqlCommand("SELECT HotelName, Latitude, Longitude FROM HotelLocations WHERE Latitude IS NOT NULL AND Longitude IS NOT NULL", sqlConn);
                using (var reader = hotelCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        try
                        {
                            hotels.Add(new HotelPoint
                            {
                                HotelName = reader["HotelName"]?.ToString(),
                                Latitude = Convert.ToDouble(reader["Latitude"]),
                                Longitude = Convert.ToDouble(reader["Longitude"])
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error parsing hotel record: {ex.Message}");
                        }
                    }
                }



                var gasCmd = new SqlCommand("SELECT CompanyBrand, Latitude, Longitude FROM GasStation WHERE Latitude IS NOT NULL AND Longitude IS NOT NULL", sqlConn);
                using (var reader = gasCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        try
                        {
                            gasStations.Add(new GasStationPoint
                            {
                                CompanyBrand = reader["CompanyBrand"]?.ToString(),
                                Latitude = double.Parse(reader["Latitude"].ToString(), CultureInfo.InvariantCulture),
                                Longitude = double.Parse(reader["Longitude"].ToString(), CultureInfo.InvariantCulture)
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error parsing gas station record: {ex.Message}");
                        }
                    }
                }



                var evCmd = new SqlCommand("SELECT CompanyBrand, Latitude, Longitude FROM EvStation WHERE Latitude IS NOT NULL AND Longitude IS NOT NULL", sqlConn);
                using (var reader = evCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        try
                        {
                            evStations.Add(new EvStationPoint
                            {
                                CompanyBrand = reader["CompanyBrand"]?.ToString(),
                                Latitude = double.Parse(reader["Latitude"].ToString(), CultureInfo.InvariantCulture),
                                Longitude = double.Parse(reader["Longitude"].ToString(), CultureInfo.InvariantCulture)
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error parsing EV station record: {ex.Message}");
                        }
                    }
                }
            }

            IDataView dataView = mlContext.Data.LoadFromEnumerable(gridData);

            // 3. Split dataset into training and test sets
            var split = mlContext.Data.TrainTestSplit(dataView, testFraction: 0.2);

            // 4. Build regression pipeline (FastTree)
            var pipeline = mlContext.Transforms.Concatenate("Features",
                                nameof(GridData.HotelDensity),
                                nameof(GridData.EVChargers),
                                nameof(GridData.GasStations),
                                nameof(GridData.InfrastructureGap))
                           .Append(mlContext.Regression.Trainers.FastTree());

            // 5. Train the model
            var model = pipeline.Fit(split.TrainSet);

            // 6. Evaluate model performance
            var predictions = model.Transform(split.TestSet);
            var metrics = mlContext.Regression.Evaluate(predictions);

            Console.WriteLine($"R²: {metrics.RSquared:F4}");
            Console.WriteLine($"RMSE: {metrics.RootMeanSquaredError:F4}");
            Console.WriteLine($"MAE: {metrics.MeanAbsoluteError:F4}");

            // 7. Predict CDI for all grid cells
            var allPredictions = model.Transform(dataView);
            var results = mlContext.Data.CreateEnumerable<GridPrediction>(allPredictions, reuseRowObject: false).ToList();

            // Optional: Output top 10 priority areas
            int counter = 1;
            foreach (var r in results.OrderByDescending(x => x.PredictedCDI).Take(10))
            {
                Console.WriteLine($"Priority {counter++}: Predicted CDI = {r.PredictedCDI:F4}");
            }

            // 8. Combine predictions with grid coordinates for mapping
            var gridCells = gridData.Zip(results, (grid, pred) => new GridCell
            {
                GridX = grid.GridX,
                GridY = grid.GridY,
                PredictedCDI = pred.PredictedCDI,
                Longitude = grid.GridX * 0.0045 + 0.0045 / 2.0,
                Latitude = grid.GridY * 0.0045 + 0.0045 / 2.0
            }).ToList();

            // 9. Load roads GeoJSON (optional, for visualization)
            string roadsGeoJsonPath = @"major_greek_roads.geojson";
            string roadsGeoJsonContent = File.ReadAllText(roadsGeoJsonPath)
                                             .Replace(Environment.NewLine, " ");

            // 10. Generate HTML Leaflet map
            MapGenerator.GenerateLeafletMapWithLayers(gridCells, hotels, gasStations, evStations, @"EV_CDI_Map.html", roadsGeoJsonContent);

            Console.WriteLine("Map Generated Successfully.");

            // Automatically open in default browser
            string fullPath = Path.GetFullPath(@"EV_CDI_Map.html");
            Process.Start(new ProcessStartInfo
            {
                FileName = fullPath,
                UseShellExecute = true
            });
        }
    }
}
