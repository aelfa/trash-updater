using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Trash.Config;
using Trash.Radarr.Api;
using Trash.Radarr.CustomFormat.Models;
using Trash.Radarr.CustomFormat.Models.Cache;
using Trash.Radarr.CustomFormat.Processors.Persistence;

namespace Trash.Radarr.CustomFormat.Processors
{
    internal class PersistenceProcessor : IPersistenceProcessor
    {
        private readonly IRadarrApi _api;
        private readonly RadarrConfiguration _config;
        private ProcessorContainer? _processors;

        public PersistenceProcessor(IRadarrApi api, IServiceConfiguration config)
        {
            _api = api;
            _config = (RadarrConfiguration) config;
            Reset();
        }

        private ProcessorContainer Processors
            => _processors ?? throw new NullReferenceException("ProcessorContainer is not initialized");

        public IReadOnlyCollection<CustomFormatTransaction> ApiTransactions
            => Processors.JsonTransactionProcessor.ApiTransactions;

        public int UpdatedCount
            => Processors.CustomFormatCustomFormatApiPersister.UpdatedCount;

        public IReadOnlyCollection<CustomFormatResponse> Responses
            => Processors.CustomFormatCustomFormatApiPersister.Responses;

        public async Task PersistCustomFormats(IReadOnlyCollection<ProcessedCustomFormatData> guideCfs,
            IReadOnlyCollection<TrashIdMapping> deletedCfsInCache)
        {
            var radarrCfs = await _api.GetCustomFormats();

            // Step 1: Match CFs between the guide & Radarr and merge the data. The goal is to retain as much of the
            // original data from Radarr as possible. There are many properties in the response JSON that we don't
            // directly care about. We keep those and just update the ones we do care about.
            Processors.JsonTransactionProcessor.Process(guideCfs, radarrCfs);

            // Step 1.1: Optionally record deletions of custom formats in cache but not in the guide
            if (_config.DeleteOldCustomFormats)
            {
                Processors.JsonTransactionProcessor.RecordDeletions(deletedCfsInCache, radarrCfs);
            }

            // Step 2: For each merged CF, persist it to Radarr via its API. This will involve a combination of updates
            // to existing CFs and creation of brand new ones, depending on what's already available in Radarr.
            await Processors.CustomFormatCustomFormatApiPersister.Process(_api,
                Processors.JsonTransactionProcessor.ApiTransactions);
        }

        public async Task SetQualityProfileScores(
            IDictionary<string, List<QualityProfileCustomFormatScoreEntry>> profileScores)
        {
            // Step 4: Update all quality profiles with the scores from the guide for the uploaded custom formats
            await Processors.ProfileQualityProfileApiPersister.Process(_api, profileScores);
        }

        public void Reset()
        {
            _processors = new ProcessorContainer();
        }

        private class ProcessorContainer
        {
            public JsonTransactionProcessor JsonTransactionProcessor { get; } = new();
            public CustomFormatApiPersistenceProcessor CustomFormatCustomFormatApiPersister { get; } = new();
            public QualityProfileApiPersistenceProcessor ProfileQualityProfileApiPersister { get; } = new();
        }
    }
}
