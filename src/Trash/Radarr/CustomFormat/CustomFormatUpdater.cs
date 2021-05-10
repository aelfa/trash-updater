using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using Trash.Command;
using Trash.Extensions;
using Trash.Radarr.CustomFormat.Models;
using Trash.Radarr.CustomFormat.Processors;

namespace Trash.Radarr.CustomFormat
{
    internal class CustomFormatUpdater : ICustomFormatUpdater
    {
        private readonly ICachePersister _cache;
        private readonly IGuideProcessor _guideProcessor;
        private readonly IPersistenceProcessor _persistenceProcessor;

        public CustomFormatUpdater(
            ILogger log,
            ICachePersister cache,
            IGuideProcessor guideProcessor,
            IPersistenceProcessor persistenceProcessor)
        {
            Log = log;
            _cache = cache;
            _guideProcessor = guideProcessor;
            _persistenceProcessor = persistenceProcessor;
        }

        private ILogger Log { get; }

        public async Task Process(IServiceCommand args, RadarrConfiguration config)
        {
            _cache.Load();

            await _guideProcessor.BuildGuideData(config.CustomFormats, _cache.CfCache);

            if (!ValidateGuideDataAndCheckShouldProceed(config))
            {
                return;
            }

            if (args.Preview)
            {
                PreviewCustomFormats();
                return;
            }

            await _persistenceProcessor.PersistCustomFormats(_guideProcessor.ProcessedCustomFormats,
                _guideProcessor.DeletedCustomFormatsInCache);

            PrintApiStatistics(args, _persistenceProcessor.Responses);

            await _persistenceProcessor.SetQualityProfileScores(_guideProcessor.ProfileScores);

            Log.Information("Updated {ProfileCount} profiles and a total of {ScoreCount} scores",
                _guideProcessor.ProfileScores.Keys.Count, _guideProcessor.ProfileScores.Sum(kvp => kvp.Value.Count));

            // Step 3: Cache all the custom formats (using ID from API response). In addition, re-assign cache entries
            // in all of the processed CFs. This captures the new cache entries, if any.
            _cache.Update(_persistenceProcessor.Responses, _guideProcessor.ProcessedCustomFormats);

            _cache.Save();

            _persistenceProcessor.Reset();
            _guideProcessor.Reset();
        }

        private void PrintApiStatistics(IServiceCommand args, IReadOnlyCollection<CustomFormatResponse> responses)
        {
            var created = responses.Where(r => r.Operation == ApiOperation.Create).ToList();
            if (created.Count > 0)
            {
                Log.Information("Created {Count} New Custom Formats:", created.Count);
                Log.Information("{CustomFormats}", created.Select(r => r.CustomFormatName));
            }

            var updated = responses.Where(r => r.Operation == ApiOperation.Update).ToList();
            if (updated.Count > 0)
            {
                Log.Information("Updated {Count} Existing Custom Formats:", updated.Count);
                Log.Information("{CustomFormats}", updated.Select(r => r.CustomFormatName));
            }

            if (args.Debug)
            {
                var skipped = responses.Where(r => r.Operation == ApiOperation.NoChange).ToList();
                if (skipped.Count > 0)
                {
                    Log.Debug("Skipped {Count} Custom Formats that did not change:", skipped.Count);
                    Log.Debug("{CustomFormats}", skipped.Select(r => r.CustomFormatName));
                }
            }

            Log.Information("Done: updated {Count} custom formats in Radarr", created.Count + updated.Count);
        }

        private bool ValidateGuideDataAndCheckShouldProceed(RadarrConfiguration config)
        {
            if (_guideProcessor.CustomFormatsNotInGuide.Count > 0)
            {
                Log.Warning("The Custom Formats below do not exist in the guide and will " +
                            "be skipped. Names must match the 'name' field in the actual JSON, not the header in " +
                            "the guide! Either fix the names or remove them from your YAML config to resolve this " +
                            "warning");
                Log.Warning("{CfList}", _guideProcessor.CustomFormatsNotInGuide);
            }

            var cfsWithoutQualityProfiles = _guideProcessor.ConfigData
                .Where(d => d.QualityProfiles.Count == 0)
                .SelectMany(d => d.CustomFormats.Select(cf => cf.Name))
                .ToList();

            if (cfsWithoutQualityProfiles.Count > 0)
            {
                Log.Debug("These custom formats will be uploaded but are not associated to a quality profile in the " +
                          "config file: {UnassociatedCfs}", cfsWithoutQualityProfiles);
            }

            // No CFs are defined in this item, or they are all invalid. Skip this whole instance.
            if (_guideProcessor.ConfigData.Count == 0)
            {
                Log.Error("Guide processing yielded no custom formats for configured instance host {BaseUrl}",
                    config.BaseUrl);
                return false;
            }

            if (_guideProcessor.CustomFormatsWithoutScore.Count > 0)
            {
                Log.Warning("The below custom formats have no score in the guide or YAML " +
                            "config and will be skipped (remove them from your config or specify a " +
                            "score to fix this warning)");
                Log.Warning("{CfList}", _guideProcessor.CustomFormatsWithoutScore);
            }

            return true;
        }

        private void PreviewCustomFormats()
        {
            Console.WriteLine("");
            Console.WriteLine("=========================================================");
            Console.WriteLine("            >>> Custom Formats From Guide <<<            ");
            Console.WriteLine("=========================================================");
            Console.WriteLine("");

            const string format = "{0,-30} {1,-35}";
            Console.WriteLine(format, "Custom Format", "Trash ID");
            Console.WriteLine(string.Concat(Enumerable.Repeat('-', 1 + 30 + 35)));

            foreach (var cf in _guideProcessor.ProcessedCustomFormats)
            {
                Console.WriteLine(format, cf.Name, cf.TrashId);
            }

            Console.WriteLine("");
            Console.WriteLine("=========================================================");
            Console.WriteLine("      >>> Quality Profile Assignments & Scores <<<       ");
            Console.WriteLine("=========================================================");
            Console.WriteLine("");

            const string profileFormat = "{0,-18} {1,-20} {2,-8}";
            Console.WriteLine(profileFormat, "Profile", "Custom Format", "Score");
            Console.WriteLine(string.Concat(Enumerable.Repeat('-', 2 + 18 + 20 + 8)));

            foreach (var (profileName, scoreEntries) in _guideProcessor.ProfileScores)
            {
                Console.WriteLine(profileFormat, profileName, "", "");

                foreach (var scoreEntry in scoreEntries)
                {
                    var matchingCf = _guideProcessor.ProcessedCustomFormats
                        .FirstOrDefault(cf => cf.TrashId.EqualsIgnoreCase(scoreEntry.CustomFormat.TrashId));

                    if (matchingCf == null)
                    {
                        Log.Warning("Quality Profile refers to CF not found in guide: {TrashId}",
                            scoreEntry.CustomFormat.TrashId);
                        continue;
                    }

                    Console.WriteLine(profileFormat, "", matchingCf.Name, scoreEntry.Score);
                }
            }

            Console.WriteLine("");
        }
    }
}
