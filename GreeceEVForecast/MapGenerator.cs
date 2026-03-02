using System.Diagnostics;
using System.Globalization;

namespace GreeceEVForecast
{
    public class MapGenerator
    {
        private static int cellIncreaseId = 0;
        public static void GenerateLeafletMapWithLayers(
            IEnumerable<GridCell> gridCells,
            IEnumerable<HotelPoint> hotels,
            IEnumerable<GasStationPoint> gasStations,
            IEnumerable<EvStationPoint> evStations,
            string outputHtmlPath,
            string roadsGeoJsonContent
        )
        {
            using (var writer = new StreamWriter(outputHtmlPath))
            {
                writer.WriteLine(@"
<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8' />
<title>EV Infrastructure CDI Map</title>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<link rel='stylesheet' href='https://unpkg.com/leaflet/dist/leaflet.css' />
<script src='https://unpkg.com/leaflet/dist/leaflet.js'></script>
<style>
#map { height: 100vh; }
.legend {
    background: white;
    padding: 10px;
    line-height: 1.5;
    border-radius: 6px;
    box-shadow: 0 0 15px rgba(0,0,0,0.2);
    font-size: 13px;
}
.legend i {
    width: 18px;
    height: 18px;
    float: left;
    margin-right: 8px;
    opacity: 0.8;
}
.circle-i { border-radius: 50%; }
</style>
</head>
<body>
<div id='map'></div>
<script>
var map = L.map('map').setView([37.98, 23.72], 7);

L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '&copy; OpenStreetMap contributors'
}).addTo(map);

// Layers
var gridLayer = L.layerGroup().addTo(map);
var hotelLayer = L.layerGroup();
var gasLayer = L.layerGroup();
var evLayer = L.layerGroup();

var motorwayLayer = L.layerGroup();
var trunkLayer = L.layerGroup();
var primaryLayer = L.layerGroup();
");

                // ================= GRID =================
                foreach (var cell in gridCells)
                {
                    cellIncreaseId++;
                    double half = 0.0045 / 2.0;
                    double south = cell.Latitude - half;
                    double north = cell.Latitude + half;
                    double west = cell.Longitude - half;
                    double east = cell.Longitude + half;

                    var hotelsInside = hotels
                        .Where(e => e.Latitude >= south && e.Latitude <= north && e.Longitude >= west && e.Longitude <= east)
                        .GroupBy(e => new { e.Latitude, e.Longitude })
                        .Select(group => group.First());

                    var gasInside = gasStations
                        .Where(g => g.Latitude >= south && g.Latitude <= north && g.Longitude >= west && g.Longitude <= east)
                        .GroupBy(g => new { g.Latitude, g.Longitude })
                        .Select(group => group.First());

                    var evInside = evStations
                        .Where(e => e.Latitude >= south && e.Latitude <= north && e.Longitude >= west && e.Longitude <= east)
                        .GroupBy(e => new { e.Latitude, e.Longitude })
                        .Select(group => group.First());


                    // Build the popup content
                    string popupContent = $"<b>Cell ID: {cellIncreaseId} - Predicted CDI: {cell.PredictedCDI:F4}</b><br/>";

                    if (hotelsInside.Any())
                    {
                        popupContent += "<br/><b>Hotels:</b> " + hotelsInside.Count();
                    }
                    else
                    {
                        popupContent += "<br/><b>EV Stations: 0</b>";
                    }
                    if (gasInside.Any())
                    {
                        popupContent += "<br/><b>Gas Stations:</b> " + gasInside.Count();
                    }
                    else
                    {
                        popupContent += "<br/><b>Gas Stations: 0</b>";
                    }
                    if (evInside.Any())
                    {
                        popupContent += "<br/><b>EV Stations:</b> " + evInside.Count();
                    }
                    else
                    {
                        popupContent += "<br/><b>EV Stations: 0</b>";
                    }

                    string safePopup = EscapeJs(popupContent);
                    writer.WriteLine($@"
L.rectangle([
    [{south.ToString(CultureInfo.InvariantCulture)}, {west.ToString(CultureInfo.InvariantCulture)}],
    [{north.ToString(CultureInfo.InvariantCulture)}, {east.ToString(CultureInfo.InvariantCulture)}]
], {{
    color: getColor({cell.PredictedCDI.ToString(CultureInfo.InvariantCulture)}),
    weight: 1,
    fillOpacity: 0.6
}})
.bindPopup(""{safePopup}"")
.addTo(gridLayer);
");
                }

                // ================= HOTELS =================
                foreach (var h in hotels)
                {
                    string safeHotel = EscapeJs($"Hotel: {h.HotelName}");
                    writer.WriteLine($@"
L.circleMarker([{h.Latitude.ToString(CultureInfo.InvariantCulture)}, {h.Longitude.ToString(CultureInfo.InvariantCulture)}], {{
    radius: 3,
    color: 'magenta',
    fillColor: 'magenta',
    fillOpacity: 0.9
}}).bindPopup(""{safeHotel}"").addTo(hotelLayer);
");
                }

                // ================= GAS =================
                foreach (var g in gasStations)
                {
                    string safeGas = EscapeJs($"Gas: {g.CompanyBrand}");
                    writer.WriteLine($@"
L.circleMarker([{g.Latitude.ToString(CultureInfo.InvariantCulture)}, {g.Longitude.ToString(CultureInfo.InvariantCulture)}], {{
    radius: 3,
    color: 'blue',
    fillColor: 'blue',
    fillOpacity: 0.9
}}).bindPopup(""{safeGas}"").addTo(gasLayer);
");
                }

                // ================= EV =================
                foreach (var e in evStations)
                {
                    string safeEv = EscapeJs($"EV: {e.CompanyBrand}");
                    writer.WriteLine($@"
L.circleMarker([{e.Latitude.ToString(CultureInfo.InvariantCulture)}, {e.Longitude.ToString(CultureInfo.InvariantCulture)}], {{
    radius: 3,
    color: 'green',
    fillColor: 'green',
    fillOpacity: 1
}}).bindPopup(""{safeEv}"").addTo(evLayer);
");
                }

                // ================= ROADS =================
                writer.WriteLine($@"
var roadsGeoJson = {roadsGeoJsonContent};

L.geoJSON(roadsGeoJson, {{
    filter: function(f) {{ return f.properties.highway === 'motorway'; }},
    style: function(f) {{ return {{ color:'red', weight:3, opacity:0.9 }}; }}
}}).addTo(motorwayLayer);

L.geoJSON(roadsGeoJson, {{
    filter: function(f) {{ return f.properties.highway === 'trunk'; }},
    style: function(f) {{ return {{ color:'yellow', weight:3, opacity:0.9 }}; }}
}}).addTo(trunkLayer);

L.geoJSON(roadsGeoJson, {{
    filter: function(f) {{ return f.properties.highway === 'primary'; }},
    style: function(f) {{ return {{ color:'orange', weight:3, opacity:0.9 }}; }}
}}).addTo(primaryLayer);
");

                // ================= CHECKBOXES =================
                writer.WriteLine(@"
var overlays = {
    'Hotels': hotelLayer,
    'Gas Stations': gasLayer,
    'EV Chargers': evLayer,
    'Motorways': motorwayLayer,
    'Trunks': trunkLayer,
    'Primary Roads': primaryLayer
};

L.control.layers(null, overlays, { collapsed: false }).addTo(map);

// Legends / Appendix
var legend = L.control({position: 'bottomleft'});
legend.onAdd = function(map) {
    var div = L.DomUtil.create('div', 'legend');
    div.innerHTML += '<b>Infrastructure Index</b><br>';
    div.innerHTML += '<i class=""circle-i"" style=""background:green""></i> EV Station<br>';
    div.innerHTML += '<i class=""circle-i"" style=""background:blue""></i> Gas Station<br>';
    div.innerHTML += '<i class=""circle-i"" style=""background:magenta""></i> Hotel<br>';
    div.innerHTML += '<i class=""circle-i"" style=""background:red""></i> Motorway<br>';
    div.innerHTML += '<i class=""circle-i"" style=""background:yellow""></i> Trunk Road<br>';
    div.innerHTML += '<i class=""circle-i"" style=""background:orange""></i> Primary Road<br>';
    return div;
};
legend.addTo(map);

// ================= CDI LEGEND =================
var cdiLegend = L.control({position: 'bottomright'});

cdiLegend.onAdd = function(map) {
    var div = L.DomUtil.create('div', 'legend');
    div.innerHTML += '<b>Predicted CDI</b><br>';
    div.innerHTML += '<i style=""background:#800026""></i> 0.8+<br>';
    div.innerHTML += '<i style=""background:#BD0026""></i> 0.6 - 0.8<br>';
    div.innerHTML += '<i style=""background:#E31A1C""></i> 0.4 - 0.6<br>';
    div.innerHTML += '<i style=""background:#FC4E2A""></i> 0.2 - 0.4<br>';
    div.innerHTML += '<i style=""background:#FFEDA0""></i> 0 - 0.2<br>';
    return div;
};

cdiLegend.addTo(map);

// CDI color function
function getColor(d) {
    return d > 0.8 ? '#800026' :
           d > 0.6 ? '#BD0026' :
           d > 0.4 ? '#E31A1C' :
           d > 0.2 ? '#FC4E2A' :
                     '#FFEDA0';
}
</script>
</body>
</html>
");
            }
        }

        private static string EscapeJs(string text)
        {
            return text.Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "");
        }
    }
}