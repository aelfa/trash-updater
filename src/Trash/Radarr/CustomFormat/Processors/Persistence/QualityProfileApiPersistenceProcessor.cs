using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Trash.Radarr.Api;
using Trash.Radarr.CustomFormat.Models;

namespace Trash.Radarr.CustomFormat.Processors.Persistence
{
    public class QualityProfileApiPersistenceProcessor
    {
        public async Task Process(IRadarrApi api,
            IDictionary<string, List<QualityProfileCustomFormatScoreEntry>> cfScores)
        {
            var response = await api.GetQualityProfiles();
            var profiles = response.Children();

            var profileScores = profiles["items"].Children().Join(cfScores,
                p => p["name"].Value<string>(),
                s => s.Key,
                (p, s) => (s.Value, p["formatItems"].Children<JObject>(), (JObject) p),
                StringComparer.InvariantCultureIgnoreCase);
            // .SelectMany(ps => ps.Score
            //     .Select(s => (Score: s,
            //         Json: ps.Json.FirstOrDefault(j
            //             => s.CustomFormat.CacheEntry != null &&
            //                j["format"].Value<int>() == s.CustomFormat.CacheEntry.CustomFormatId))))
            foreach (var (scoreList, jsonList, jsonRoot) in profileScores)
            {
                JObject FindJsonScoreEntry(QualityProfileCustomFormatScoreEntry score)
                {
                    return jsonList.First(j
                        => score.CustomFormat.CacheEntry != null &&
                           j["format"].Value<int>() == score.CustomFormat.CacheEntry.CustomFormatId);
                }

                foreach (var (score, json) in scoreList.Select(s => (Score: s, Json: FindJsonScoreEntry(s))))
                {
                    json["score"] = score.Score;
                }

                await api.UpdateQualityProfile(jsonRoot, jsonRoot["id"].Value<int>());
            }
        }
    }
}
