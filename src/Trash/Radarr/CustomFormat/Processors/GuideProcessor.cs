using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Serilog;
using Trash.Cache;
using Trash.Radarr.CustomFormat.Guide;
using Trash.Radarr.CustomFormat.Models;
using Trash.Radarr.CustomFormat.Models.Cache;
using Trash.Radarr.CustomFormat.Processors.Guide;

namespace Trash.Radarr.CustomFormat.Processors
{
    internal class GuideProcessor : IGuideProcessor
    {
        private readonly IServiceCache _cache;
        private readonly ICustomFormatGuideParser _guideParser;
        private IList<CustomFormatData>? _guideData;
        private ProcessorContainer? _processors;

        public GuideProcessor(ILogger log, ICustomFormatGuideParser guideParser, IServiceCache cache)
        {
            _guideParser = guideParser;
            _cache = cache;
            Log = log;
            Reset();
        }

        private ProcessorContainer Processors
            => _processors ?? throw new NullReferenceException("ProcessorContainer is not initialized");

        private ILogger Log { get; }

        public IReadOnlyCollection<ProcessedCustomFormatData> ProcessedCustomFormats
            => Processors.CustomFormat.ProcessedCustomFormats;

        public IReadOnlyCollection<string> CustomFormatsNotInGuide
            => Processors.Config.CustomFormatsNotInGuide;

        public IReadOnlyCollection<ProcessedConfigData> ConfigData
            => Processors.Config.ConfigData;

        public IDictionary<string, List<QualityProfileCustomFormatScoreEntry>> ProfileScores
            => Processors.QualityProfile.ProfileScores;

        public IReadOnlyCollection<(string name, string trashId, string profileName)> CustomFormatsWithoutScore
            => Processors.QualityProfile.CustomFormatsWithoutScore;

        public IReadOnlyCollection<TrashIdMapping> DeletedCustomFormatsInCache
            => Processors.CustomFormat.DeletedCustomFormatsInCache;

        public async Task BuildGuideData(IReadOnlyList<CustomFormatConfig> config)
        {
            if (_guideData == null)
            {
                Log.Debug("Requesting and parsing guide markdown");
                var markdownData = await _guideParser.GetMarkdownData();
                _guideData = _guideParser.ParseMarkdown(markdownData);
            }

            // Grab the cache if one is available
            var cache = _cache.Load<CustomFormatCache>();
            if (cache == null)
            {
                Log.Debug("Custom format cache does not exist; proceeding without it");
            }

            // Step 1: Process and filter the custom formats from the guide.
            // Custom formats in the guide not mentioned in the config are filtered out.
            Processors.CustomFormat.Process(_guideData, config, cache);

            // todo: Process cache entries that do not exist in the guide. Those should be deleted
            // This might get taken care of when we rebuild the cache based on what is actually updated when
            // we call the Radarr API

            // Step 2: Use the processed custom formats from step 1 to process the configuration.
            // CFs in config not in the guide are filtered out.
            // Actual CF objects are associated to the quality profile objects to reduce lookups
            Processors.Config.Process(Processors.CustomFormat.ProcessedCustomFormats, config);

            // Step 3: Use the processed config (which contains processed CFs) to process the quality profile scores.
            // Score precedence logic is utilized here to decide the CF score per profile (same CF can actually have
            // different scores depending on which profile it goes into).
            Processors.QualityProfile.Process(Processors.Config.ConfigData);
        }

        public void Reset()
        {
            _processors = new ProcessorContainer();
        }

        private class ProcessorContainer
        {
            public CustomFormatProcessor CustomFormat { get; } = new();
            public ConfigProcessor Config { get; } = new();
            public QualityProfileProcessor QualityProfile { get; } = new();
        }
    }
}
