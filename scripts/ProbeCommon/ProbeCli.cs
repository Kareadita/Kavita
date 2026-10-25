namespace ProbeCommon;

public static class ProbeCli
{
    public static Dictionary<string, string?> ParseOptions(string[] rest)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < rest.Length; i++)
        {
            if (!rest[i].StartsWith("--")) continue;
            var key = rest[i][2..];
            var hasValue = i + 1 < rest.Length && !rest[i + 1].StartsWith("--");
            result[key] = hasValue ? rest[++i] : null;
        }
        return result;
    }

    public static void PrintEnvironment(string tool, EnvironmentInfo environment)
    {
        Console.WriteLine($"{tool} v{environment.ProbeVersion} {environment.Mode} on {environment.Path}");
        Console.WriteLine($"  {environment.Os} | {environment.Rid} | container: {environment.InContainer}");
        Console.WriteLine($"  filesystem: {environment.FileSystem ?? "?"} {environment.DriveType} | mount: {environment.MountPoint} {environment.MountOptions}");
        if (environment.MountError != null)
        {
            Console.WriteLine($"  mount: {environment.MountError}");
        }
        if (environment.DriveInfoName != null || environment.DriveInfoFormat != null)
        {
            Console.WriteLine($"  drive info: {environment.DriveInfoName} {environment.DriveInfoFormat}");
        }
    }

    /// <returns><paramref name="outPath"/>, or a timestamped file next to the program</returns>
    public static string ReportPath(string tool, EnvironmentInfo environment, string? outPath) =>
        outPath ?? Path.Combine(AppContext.BaseDirectory,
            $"{tool}-{environment.Mode}-{Sanitize(environment.Label ?? Environment.MachineName)}-{DateTime.Now:yyyyMMdd-HHmmss}.json");

    /// <returns>Where the report was written</returns>
    public static string SaveReport(string tool, EnvironmentInfo environment, string? outPath, string content)
    {
        var path = ReportPath(tool, environment, outPath);
        File.WriteAllText(path, content);
        return path;
    }

    private static string Sanitize(string value) =>
        string.Concat(value.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-'));
}
