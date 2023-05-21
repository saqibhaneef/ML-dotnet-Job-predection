﻿using Microsoft.Extensions.ML;
using Microsoft.ML;
using Microsoft.ML.Data;
using ML.JobPrediction.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ML.JobPrediction.Core
{
    public class JobPredictionLabelService
    {
        private readonly MLContext _mlContext;
        private readonly PredictionEnginePool<Job, Models.JobPredictionModel> _predictionEnginePool;

        private ITransformer _mlModel;
        private List<string> _categories;

        public JobPredictionLabelService(MLContext mlContext)
        {
            // Use this when trying to load models manually.
            _mlContext = mlContext;
        }

        public JobPredictionLabelService(PredictionEnginePool<Job, Models.JobPredictionModel> predictionEnginePool)
        {
            // Use this when using ML.NET in WebAPI, Azure Functions and other scalable applications.
            _predictionEnginePool = predictionEnginePool;
        }

        public void LoadModelFromFile(string modelPath)
        {
            // Load model from file.
            using (var stream = new FileStream(modelPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                SetModel(_mlContext.Model.Load(stream, out var _));
        }

        public void LoadModelFromStream(Stream modelStream)
        {
            // Load model from file.
            SetModel(_mlContext.Model.Load(modelStream, out var _));
        }

        public void SetModel(ITransformer mlModel)
        {
            _categories = null;
            _mlModel = mlModel;
        }

        public string PredictCategory(Job transaction)
        {
            var prediction = Predict(transaction);
            return prediction?.Category;
        }

        public Models.JobPredictionModel Predict(Job transaction)
        {
            if (_predictionEnginePool != null)
            {
                // Used for scalable applications.
                return _predictionEnginePool.Predict(transaction);
            }

            // Used for console applications where multi-threading might not be a problem.
            var predictionEngine = _mlContext.Model.CreatePredictionEngine<Job, JobPredictionModel>(_mlModel);
            return predictionEngine.Predict(transaction);
        }

        public List<string> GetCategories()
        {
            if (_categories != null)
            {
                return _categories;
            }

            // Based on https://github.com/dotnet/docs/issues/14265
            var schema = GetOutputSchema();
            var column = schema.GetColumnOrNull("Score");

            var slotNames = new VBuffer<ReadOnlyMemory<char>>();
            column.Value.GetSlotNames(ref slotNames);
            var names = new string[slotNames.Length];

            _categories = slotNames
                .DenseValues()
                .Select(x => x.ToString())
                .ToList();

            return _categories;
        }

        public DataViewSchema GetOutputSchema()
        {
            PredictionEngine<Job, JobPredictionModel> predEngine = _predictionEnginePool != null
                ? _predictionEnginePool.GetPredictionEngine()
                : _mlContext.Model.CreatePredictionEngine<Job, JobPredictionModel>(_mlModel);

            return predEngine.OutputSchema;
        }

        public static Dictionary<string, float> GetScoresWithLabelsSorted(DataViewSchema schema, string name, float[] scores)
        {
            // Based on https://github.com/dotnet/docs/issues/14265
            Dictionary<string, float> result = new Dictionary<string, float>();

            var column = schema.GetColumnOrNull(name);

            var slotNames = new VBuffer<ReadOnlyMemory<char>>();
            column.Value.GetSlotNames(ref slotNames);
            var names = new string[slotNames.Length];
            var num = 0;
            foreach (var denseValue in slotNames.DenseValues())
            {
                result.Add(denseValue.ToString(), scores[num++]);
            }

            return result.OrderByDescending(c => c.Value).ToDictionary(i => i.Key, i => i.Value);
        }
    }
}
