using System.IO;

namespace Kavita.Common.Extensions;

public static class PathExtensions
{
    public static string GetParentDirectory(string filePath)
    {
        return Path.GetDirectoryName(filePath);
    }
}
