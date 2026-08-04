using System;
using System.IO;
using Kavita.Services.Kobo;

namespace Kavita.Services.Tests.Kobo;

public class KepubifyPathResolverTests
{
    [Fact]
    public void Resolve_PrefersConfiguredPath_WhenFileExists()
    {
        using var root = new TempDir();
        var overridePath = Path.Combine(root.Path, "custom-kepubify");
        File.WriteAllText(overridePath, "x");
        Directory.CreateDirectory(Path.Combine(root.Path, "tools"));
        File.WriteAllText(Path.Combine(root.Path, "tools", "kepubify"), "bundled");

        var resolver = new KepubifyPathResolver(root.Path);
        var resolved = resolver.Resolve(overridePath);

        Assert.Equal(Path.GetFullPath(overridePath), resolved);
    }

    [Fact]
    public void Resolve_FallsBackToBundled_WhenOverrideMissing()
    {
        using var root = new TempDir();
        var tools = Path.Combine(root.Path, "tools");
        Directory.CreateDirectory(tools);
        var bundled = Path.Combine(tools, OperatingSystem.IsWindows() ? "kepubify.exe" : "kepubify");
        File.WriteAllText(bundled, "bundled");

        var resolver = new KepubifyPathResolver(root.Path);
        var resolved = resolver.Resolve("/does/not/exist/kepubify");

        Assert.Equal(Path.GetFullPath(bundled), resolved);
    }

    [Fact]
    public void Resolve_FallsBackToBundled_WhenOverrideEmpty()
    {
        using var root = new TempDir();
        var tools = Path.Combine(root.Path, "tools");
        Directory.CreateDirectory(tools);
        var bundled = Path.Combine(tools, OperatingSystem.IsWindows() ? "kepubify.exe" : "kepubify");
        File.WriteAllText(bundled, "bundled");

        var resolver = new KepubifyPathResolver(root.Path);
        var resolved = resolver.Resolve("  ");

        Assert.Equal(Path.GetFullPath(bundled), resolved);
    }

    [Fact]
    public void Resolve_UsesPathEnv_WhenBundledMissing()
    {
        using var root = new TempDir();
        using var pathDir = new TempDir();
        var onPath = Path.Combine(pathDir.Path, OperatingSystem.IsWindows() ? "kepubify.exe" : "kepubify");
        File.WriteAllText(onPath, "path");

        var previousPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", pathDir.Path);
            var resolver = new KepubifyPathResolver(root.Path);
            var resolved = resolver.Resolve(null);

            Assert.Equal(Path.GetFullPath(onPath), resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previousPath);
        }
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenNothingFound()
    {
        using var root = new TempDir();
        var previousPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", root.Path);
            var resolver = new KepubifyPathResolver(root.Path);
            Assert.Null(resolver.Resolve(null));
            Assert.Null(resolver.Resolve("/missing/kepubify"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previousPath);
        }
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "kavita-kepubify-test-" + Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }
}
