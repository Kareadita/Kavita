using System;

namespace API.Extensions;

public static class VersionExtensions
{
    public static bool CompareWithoutRevision(this Version v1, Version v2)
    {
        if (v1.Major != v2.Major)
            return v1.Major == v2.Major;
        if (v1.Minor != v2.Minor)
            return v1.Minor == v2.Minor;
        if (v1.Build != v2.Build)
            return v1.Build == v2.Build;
        return true;
    }


    /// <summary>
    /// v0.8.2.3 is within v0.8.2 (v1). Essentially checks if this is a Nightly of a stable release
    /// </summary>
    /// <param name="v1"></param>
    /// <param name="v2"></param>
    /// <returns></returns>
    public static bool IsWithinStableRelease(this Version v1, Version v2)
    {
        return v1.Major == v2.Major && v1.Minor != v2.Minor && v1.Build != v2.Build;
    }


}
