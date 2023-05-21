﻿using Microsoft.ML;
using Microsoft.ML.AutoML;
using ML.JobPrediction.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace ML.JobPrediction.Core
{
    public class JobPredictionTrainingService
    {
        private readonly MLContext _mlContext;
        private IDataView _trainingDataView;

        public JobPredictionTrainingService(MLContext mlContext)
        {
            _mlContext = mlContext;
        }

        public ITransformer ManualTrain(IEnumerable<Job> trainingData)
        {
            // Configure ML pipeline
            var pipeline = LoadDataProcessPipeline(_mlContext);
            var trainingPipeline = GetTrainingPipeline(_mlContext, pipeline);
            _trainingDataView = _mlContext.Data.LoadFromEnumerable(trainingData);

            // Generate training model.
            return trainingPipeline.Fit(_trainingDataView);
        }

        public ITransformer AutoTrain(IEnumerable<Job> trainingData, uint maxTimeInSec)
        {
            _trainingDataView = _mlContext.Data.LoadFromEnumerable(trainingData);

            var experimentSettings = new MulticlassExperimentSettings();
            experimentSettings.MaxExperimentTimeInSeconds = maxTimeInSec;
            experimentSettings.OptimizingMetric = MulticlassClassificationMetric.MacroAccuracy;

            var experiment = _mlContext.Auto().CreateMulticlassClassificationExperiment(experimentSettings);
            var columnInfo = new ColumnInformation
            {
                LabelColumnName = nameof(Job.Category)
            };
            columnInfo.TextColumnNames.Add(nameof(Job.Description));

            var result = experiment.Execute(_trainingDataView, columnInfo);
            return result.BestRun.Model;
        }

        public void SaveModel(string modelSavePath, ITransformer model)
        {
            // Save training model to disk.
            _mlContext.Model.Save(model, _trainingDataView.Schema, modelSavePath);
        }

        private IEstimator<ITransformer> LoadDataProcessPipeline(MLContext mlContext)
        {
            // Configure data pipeline based on the features in TransactionData.
            // Description and TransactionType are the inputs and Category is the expected result.
            var dataProcessPipeline = mlContext
                .Transforms.Conversion.MapValueToKey(inputColumnName: nameof(Job.Category), outputColumnName: "Label")
                .Append(mlContext.Transforms.Text.FeaturizeText(inputColumnName: nameof(Job.Description), outputColumnName: "Features"))
                .AppendCacheCheckpoint(mlContext);

            return dataProcessPipeline;
        }

        private IEstimator<ITransformer> GetTrainingPipeline(MLContext mlContext, IEstimator<ITransformer> pipeline)
        {
            // Use the multi-class SDCA algorithm to predict the label using features.
            // For StochasticDualCoordinateAscent the KeyToValue needs to be PredictedLabel.
            return pipeline
                .Append(GetScadaTrainer(mlContext))
                .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));
        }

        private Microsoft.ML.Trainers.SdcaMaximumEntropyMulticlassTrainer GetScadaTrainer(MLContext mlContext)
        {
            return mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features");
        }
    }
}
