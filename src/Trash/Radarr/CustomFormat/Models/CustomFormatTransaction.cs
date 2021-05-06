using System;
using Newtonsoft.Json.Linq;

namespace Trash.Radarr.CustomFormat.Models
{
    public class CustomFormatTransaction
    {
        private readonly int? _customFormatId;

        public CustomFormatTransaction(ApiOperation result, JObject json,
            ProcessedCustomFormatData processedCustomFormat)
        {
            ApiOperation = result;
            Json = json;
            TrashId = processedCustomFormat.TrashId;
            CustomFormatName = processedCustomFormat.CacheAwareName;

            // Don't use the CacheEntry for this because it's unreliable for two scenarios:
            // - Newly created custom formats (won't have an ID yet)
            // - Updates to custom formats found by name instead of ID (cache was missing or out of sync)
            _customFormatId = json.TryGetValue("id", out var value) ? value.Value<int>() : null;
        }

        public CustomFormatTransaction(ApiOperation result, JObject json, int customFormatId, string trashId,
            string customFormatName)
        {
            ApiOperation = result;
            Json = json;
            TrashId = trashId;
            CustomFormatName = customFormatName;
            _customFormatId = customFormatId;
        }

        public ApiOperation ApiOperation { get; }
        public JObject Json { get; }
        public string TrashId { get; }
        public string CustomFormatName { get; }

        public int GetCustomFormatId()
        {
            return _customFormatId ?? throw new ArgumentException("A custom format ID was not provided");
        }
    }
}
