using System;
using System.Collections.Generic;
using System.Text;

namespace GreeceEVForecast
{
    public class GridCell
    {
        public int GridX { get; set; }
        public int GridY { get; set; }
        public float PredictedCDI { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
