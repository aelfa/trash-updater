using System.Collections.Generic;
using System.Linq;
using Trash.Cache;
using Trash.Extensions;

namespace Trash.Radarr.CustomFormat.Models.Cache
{
    [CacheObjectName("custom-format-cache")]
    public class CustomFormatCache
    {
        public List<TrashIdMapping> TrashIdMappings { get; init; } = new();

        public TrashIdMapping? FindCacheEntry(ProcessedCustomFormatData cf)
            => TrashIdMappings.FirstOrDefault(c => c.TrashId.EqualsIgnoreCase(cf.TrashId));
    }

    public class TrashIdMapping
    {
        public string CustomFormatName { get; init; } = "";
        public string TrashId { get; init; } = "";
        public int CustomFormatId { get; init; }
    }
}
