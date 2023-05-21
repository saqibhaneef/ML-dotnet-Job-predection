using Microsoft.ML.Data;

namespace ML.JobPrediction.Core.Models
{
    public class JobPredictionModel
    {
        [ColumnName("PredictedLabel")]
        public string Category { get; set; }

        public float[] Score { get; set; }
    }
}
