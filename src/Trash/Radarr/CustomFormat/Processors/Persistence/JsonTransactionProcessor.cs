using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Trash.Extensions;
using Trash.Radarr.CustomFormat.Models;
using Trash.Radarr.CustomFormat.Models.Cache;

namespace Trash.Radarr.CustomFormat.Processors.Persistence
{
    public class JsonTransactionProcessor
    {
        public List<CustomFormatTransaction> ApiTransactions { get; } = new();

        public void Process(IReadOnlyCollection<ProcessedCustomFormatData> guideCfs,
            IReadOnlyCollection<JObject> radarrCfs)
        {
            // var byId = guideCfs.FullOuterJoin(radarrCfs,
            //     gcf => gcf.CacheEntry?.CustomFormatId, rcf => rcf["id"].Value<int>(),
            //     (gcf, rcf, _) => (GuideCf: gcf, RadarrCf: rcf));
            //
            // var byName = guideCfs.FullOuterJoin(radarrCfs,
            //     gcf => gcf.Name, rcf => rcf["name"].Value<string>(),
            //     (gcf, rcf, _) => (GuideCf: gcf, RadarrCf: rcf));

            foreach (var (guideCf, radarrCf) in guideCfs.Select(gcf
                => (GuideCf: gcf, RadarrCf: FindRadarrCf(radarrCfs, gcf))))
            {
                var guideCfJson = BuildNewRadarrCf(guideCf.Json);

                void AddTransaction(ApiOperation result, JObject json)
                {
                    ApiTransactions.Add(new CustomFormatTransaction(result, json, guideCf));
                }

                // no match; we add this CF as brand new
                if (radarrCf == null)
                {
                    AddTransaction(ApiOperation.Create, guideCfJson);
                }
                // found match in radarr CFs; update the existing CF
                else
                {
                    var radarrCfCopy = (JObject) radarrCf.DeepClone();
                    UpdateRadarrCf(radarrCfCopy, guideCfJson);

                    var operation = !JToken.DeepEquals(radarrCf, radarrCfCopy)
                        ? ApiOperation.Update
                        : ApiOperation.NoChange;

                    AddTransaction(operation, radarrCfCopy);
                }
            }
        }

        private static JObject? FindRadarrCf(IReadOnlyCollection<JObject> radarrCfs, ProcessedCustomFormatData guideCf)
        {
            return FindRadarrCf(radarrCfs, guideCf.CacheEntry?.CustomFormatId, guideCf.Name);
        }

        private static JObject? FindRadarrCf(IReadOnlyCollection<JObject> radarrCfs, int? cfId, string cfName)
        {
            // Try to find match in cache first and in guide by name second
            return radarrCfs.FirstOrDefault(rcf => cfId == rcf["id"].Value<int>()) ??
                   radarrCfs.FirstOrDefault(rcf => cfName.EqualsIgnoreCase(rcf["name"].Value<string>()));
        }

        private static void UpdateRadarrCf(JObject radarrCf, JObject guideCfJson)
        {
            MergeProperties(radarrCf, guideCfJson, JTokenType.Array);

            var radarrSpecs = radarrCf["specifications"].Children<JObject>();
            var guideSpecs = guideCfJson["specifications"].Children<JObject>();

            var matchedGuideSpecs = guideSpecs
                .GroupBy(gs => radarrSpecs.FirstOrDefault(gss => KeyMatch(gss, gs, "name")))
                .SelectMany(kvp => kvp.Select(gs => new {GuideSpec = gs, RadarrSpec = kvp.Key}));

            var newRadarrSpecs = new JArray();

            foreach (var match in matchedGuideSpecs)
            {
                if (match.RadarrSpec != null)
                {
                    MergeProperties(match.RadarrSpec, match.GuideSpec);
                    newRadarrSpecs.Add(match.RadarrSpec);
                }
                else
                {
                    newRadarrSpecs.Add(match.GuideSpec);
                }
            }

            radarrCf["specifications"] = newRadarrSpecs;
        }

        private static bool KeyMatch(JObject left, JObject right, string keyName)
            => left[keyName].Value<string>() == right[keyName].Value<string>();

        private static void MergeProperties(JObject radarrCf, JObject guideCfJson,
            JTokenType exceptType = JTokenType.None)
        {
            foreach (var guideProp in guideCfJson.Properties().Where(p => p.Value.Type != exceptType))
            {
                radarrCf[guideProp.Name] = guideProp.Value;
            }
        }

        private static JObject BuildNewRadarrCf(string jsonPayload)
        {
            // Information on required fields from nitsua
            /*
                ok, for the specs.. you need name, implementation, negate, required, fields
                for fields you need name & value
                top level you need name, includeCustomFormatWhenRenaming, specs and id (if updating)
                everything else radarr can handle with backend logic
             */

            var cf = JObject.Parse(jsonPayload);
            foreach (var child in cf["specifications"])
            {
                // convert from `"fields": {}` to `"fields": [{}]` (object to array of object)
                // Weirdly the exported version of a custom format is not in array form, but the API requires the array
                // even if there's only one element.
                var field = child["fields"];
                field["name"] = "value";
                child["fields"] = new JArray {field};
            }

            return cf;
        }

        public void RecordDeletions(IEnumerable<TrashIdMapping> deletedCfsInCache, List<JObject> radarrCfs)
        {
            // The 'Where' excludes cached CFs that were deleted manually by the user in Radarr
            var deletions = deletedCfsInCache
                .Select(del => (del, FindRadarrCf(radarrCfs, del.CustomFormatId, del.CustomFormatName)))
                .Where(pair => pair.Item2 != null)
                .Select(pair => new CustomFormatTransaction(ApiOperation.Delete, pair.Item2!, pair.Item1.CustomFormatId,
                    pair.Item1.TrashId, pair.Item1.CustomFormatName));

            ApiTransactions.AddRange(deletions);
        }
    }
}
