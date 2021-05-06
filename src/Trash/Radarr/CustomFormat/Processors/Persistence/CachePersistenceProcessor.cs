using System.Collections.Generic;
using System.Linq;
using Trash.Cache;
using Trash.Radarr.CustomFormat.Models;
using Trash.Radarr.CustomFormat.Models.Cache;

namespace Trash.Radarr.CustomFormat.Processors.Persistence
{
    public class CachePersistenceProcessor
    {
        private readonly IServiceCache _cache;

        public CachePersistenceProcessor(IServiceCache cache)
        {
            _cache = cache;
        }

        public void Process(IEnumerable<CustomFormatResponse> responses,
            IEnumerable<ProcessedCustomFormatData> customFormats)
        {
            var cfCache = new CustomFormatCache();
            foreach (var rsp in responses.Where(r => r.CustomFormatId != null))
            {
                CacheCustomFormat(cfCache, rsp);
            }

            _cache.Save(cfCache);

            foreach (var cf in customFormats)
            {
                cf.CacheEntry = cfCache.FindCacheEntry(cf);
            }
        }

        private static void CacheCustomFormat(CustomFormatCache cfCache, CustomFormatResponse response)
        {
            cfCache.TrashIdMappings.Add(new TrashIdMapping
            {
                CustomFormatId = response.CustomFormatId!.Value,
                TrashId = response.TrashId,
                CustomFormatName = response.CustomFormatName
            });
        }
    }
}
