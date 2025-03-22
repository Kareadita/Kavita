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
}
