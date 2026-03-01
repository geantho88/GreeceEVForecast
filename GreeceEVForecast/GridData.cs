using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace GreeceEVForecast
{
    public class GridData
    {
        public int GridX { get; set; }
        public int GridY { get; set; }
        public float HotelDensity { get; set; }
        public float EVChargers { get; set; }
        public float GasStations { get; set; }
        public float InfrastructureGap { get; set; }
        [ColumnName("Label")]
        public float CDI { get; set; }  // ML.NET sees it as 'Label'
    }
}
