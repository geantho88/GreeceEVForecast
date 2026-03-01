using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace GreeceEVForecast
{
    public class GridPrediction
    {
        [ColumnName("Score")]
        public float PredictedCDI { get; set; }
    }
}
