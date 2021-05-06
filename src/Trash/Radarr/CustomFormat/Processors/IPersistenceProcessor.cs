using System.Collections.Generic;
using System.Threading.Tasks;
using Trash.Radarr.CustomFormat.Models;
using Trash.Radarr.CustomFormat.Models.Cache;

namespace Trash.Radarr.CustomFormat.Processors
{
    public interface IPersistenceProcessor
    {
        IReadOnlyCollection<CustomFormatTransaction> ApiTransactions { get; }
        int UpdatedCount { get; }
        IReadOnlyCollection<CustomFormatResponse> Responses { get; }

        Task PersistCustomFormats(IReadOnlyCollection<ProcessedCustomFormatData> guideCfs,
            IReadOnlyCollection<TrashIdMapping> deletedCfsInCache);

        Task SetQualityProfileScores(IDictionary<string, List<QualityProfileCustomFormatScoreEntry>> profileScores);
        void Reset();
    }
}
