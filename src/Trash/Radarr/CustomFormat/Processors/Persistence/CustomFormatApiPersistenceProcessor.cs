using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Trash.Radarr.Api;
using Trash.Radarr.CustomFormat.Models;

namespace Trash.Radarr.CustomFormat.Processors.Persistence
{
    public class CustomFormatApiPersistenceProcessor
    {
        public int UpdatedCount { get; private set; }
        public List<CustomFormatResponse> Responses { get; } = new();

        public async Task Process(IRadarrApi api, IEnumerable<CustomFormatTransaction> cfs)
        {
            // Create new custom formats
            foreach (var cf in cfs)
            {
                JObject? responseCf = null;
                switch (cf.ApiOperation)
                {
                    case ApiOperation.Create:
                        responseCf = await api.CreateCustomFormat(cf.Json);
                        break;

                    case ApiOperation.Update:
                        responseCf = await api.UpdateCustomFormat(cf.Json, cf.GetCustomFormatId());
                        break;

                    case ApiOperation.Delete:
                        await api.DeleteCustomFormat(cf.GetCustomFormatId());
                        break;

                    case ApiOperation.NoChange:
                        break;

                    default:
                        continue;
                }

                var customFormatId = responseCf?.Property("id").Value<int>();
                Responses.Add(
                    new CustomFormatResponse(cf.ApiOperation, customFormatId, cf.TrashId, cf.CustomFormatName));

                ++UpdatedCount;
            }
        }
    }
}
