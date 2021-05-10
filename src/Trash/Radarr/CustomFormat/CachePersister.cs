using System.Collections.Generic;
using System.Linq;
using Serilog;
using Trash.Cache;
using Trash.Radarr.CustomFormat.Models;
using Trash.Radarr.CustomFormat.Models.Cache;

namespace Trash.Radarr.CustomFormat
{
    public class CachePersister : ICachePersister
    {
        private readonly IServiceCache _cache;

        public CachePersister(ILogger log, IServiceCache cache)
        {
            Log = log;
            _cache = cache;
        }

        private ILogger Log { get; }
        public CustomFormatCache? CfCache { get; private set; }

        public void Load()
        {
            CfCache = _cache.Load<CustomFormatCache>();

            // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
            if (CfCache == null)
            {
                Log.Debug("Custom format cache does not exist; proceeding without it");
            }
            else
            {
                Log.Debug("Loaded Cache");
            }
        }

        public void Save()
        {
            if (CfCache != null)
            {
                Log.Debug("Saving Cache");
                _cache.Save(CfCache);
            }
        }

        public void Update(IEnumerable<CustomFormatResponse> responses,
            IEnumerable<ProcessedCustomFormatData> customFormats)
        {
            if (CfCache == null)
            {
                Log.Warning("Attempted to update cache when it isn't loaded!");
                return;
            }

            foreach (var rsp in responses.Where(r => r.CustomFormatId != null))
            {
                CacheCustomFormat(CfCache, rsp);
            }

            foreach (var cf in customFormats)
            {
                cf.CacheEntry = CfCache.FindCacheEntry(cf);
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
