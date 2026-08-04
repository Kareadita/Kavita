using System;
using System.IO;
using Kavita.API.Services;

namespace Kavita.Services.Kobo;

/// <summary>
/// Resolves kepubify: admin override (if present on disk) → bundled <c>tools/</c> → PATH.
/// </summary>
public class KepubifyPathResolver : IKepubifyPathResolver
{
    private readonly string _baseDirectory;

    public KepubifyPathResolver() : this(AppContext.BaseDirectory)
    {
    }

    /// <summary>Test seam for a fixed install root.</summary>
    public KepubifyPathResolver(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        _baseDirectory = baseDirectory;
    }

    public string? Resolve(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var trimmed = configuredPath.Trim();
            if (File.Exists(trimmed))
            {
                return Path.GetFullPath(trimmed);
            }
        }

        var bundled = GetBundledPath();
        if (File.Exists(bundled))
        {
            return Path.GetFullPath(bundled);
        }

        return FindOnPath(BundledFileName);
    }

    public string GetBundledPath() =>
        Path.Combine(_baseDirectory, "tools", BundledFileName);

    private static string BundledFileName =>
        OperatingSystem.IsWindows() ? "kepubify.exe" : "kepubify";

    /// <summary>Searches PATH directories for an existing kepubify binary.</summary>
    internal static string? FindOnPath(string fileName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv)) return null;

        string[] extensions;
        if (OperatingSystem.IsWindows())
        {
            var pathExt = Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD";
            extensions = pathExt.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            // Prefer looking for the name as given first (may already include .exe).
            if (!fileName.Contains('.', StringComparison.Ordinal))
            {
                // Will try name + each extension below.
            }
        }
        else
        {
            extensions = [string.Empty];
        }

        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;

            if (OperatingSystem.IsWindows())
            {
                // Exact name as provided (e.g. kepubify.exe).
                var direct = Path.Combine(dir, fileName);
                if (File.Exists(direct)) return Path.GetFullPath(direct);

                var baseName = Path.GetFileNameWithoutExtension(fileName);
                foreach (var ext in extensions)
                {
                    var candidate = Path.Combine(dir, baseName + ext);
                    if (File.Exists(candidate)) return Path.GetFullPath(candidate);
                }
            }
            else
            {
                var candidate = Path.Combine(dir, fileName);
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            }
        }

        return null;
    }
}
