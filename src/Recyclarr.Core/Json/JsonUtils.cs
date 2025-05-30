using System.IO.Abstractions;
using Recyclarr.Common.Extensions;

namespace Recyclarr.Json;

public static class JsonUtils
{
    public static IEnumerable<IFileInfo> GetJsonFilesInDirectories(
        IEnumerable<IDirectoryInfo?> dirs,
        ILogger log
    )
    {
        var dirsThatExist = dirs.NotNull().ToLookup(x => x.Exists);

        foreach (var dir in dirsThatExist[false])
        {
            log.Debug("Specified metadata path does not exist: {Path}", dir);
        }

        return dirsThatExist[true].SelectMany(x => x.GetFiles("*.json"));
    }
}
