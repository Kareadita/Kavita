using Kavita.API.Services;
using Kavita.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Kavita.Services.Tests;

public class FileCacheServiceTests : IDisposable
{
    private readonly IDirectoryService _directoryService = Substitute.For<IDirectoryService>();
    private readonly ILogger<FileCacheService> _logger = Substitute.For<ILogger<FileCacheService>>();
    private readonly FileCacheService _sut;
    private readonly string _tempPath;

    private const string Bucket = "test-bucket";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public FileCacheServiceTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempPath);
        _directoryService.LongTermCacheDirectory.Returns(_tempPath);
        _sut = new FileCacheService(_directoryService, _logger);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
            Directory.Delete(_tempPath, true);
        GC.SuppressFinalize(this);
    }

    private string CachePath(string sanitizedKey) =>
        Path.Combine(_tempPath, Bucket, $"{sanitizedKey}.json");

    // ── GetOrFetchAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrFetchAsync_CacheMiss_CallsFetchAndWritesFile()
    {
        var fetchCalled = false;
        Task<string?> Fetch(CancellationToken _) { fetchCalled = true; return Task.FromResult<string?>("hello"); }

        var result = await _sut.GetOrFetchAsync("key", Bucket, Ttl, Fetch);

        Assert.True(fetchCalled);
        Assert.Equal("hello", result);
        Assert.True(File.Exists(CachePath("key")));
    }

    [Fact]
    public async Task GetOrFetchAsync_CacheHit_WithinTtl_ReturnsCachedValue_FetchNotCalled()
    {
        var path = CachePath("key");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "\"cached-value\"");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow);

        var fetchCalled = false;
        Task<string?> Fetch(CancellationToken _) { fetchCalled = true; return Task.FromResult<string?>("fresh"); }

        var result = await _sut.GetOrFetchAsync("key", Bucket, Ttl, Fetch);

        Assert.False(fetchCalled);
        Assert.Equal("cached-value", result);
    }

    [Fact]
    public async Task GetOrFetchAsync_CacheExpired_CallsFetchAndOverwritesFile()
    {
        var path = CachePath("key");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "\"old-value\"");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow - Ttl - TimeSpan.FromSeconds(10));

        var result = await _sut.GetOrFetchAsync("key", Bucket, Ttl,
            _ => Task.FromResult<string?>("new-value"));

        Assert.Equal("new-value", result);
        Assert.Contains("new-value", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task GetOrFetchAsync_CorruptedJson_FallsThroughToFetch()
    {
        var path = CachePath("key");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "NOT_VALID_JSON{{{{");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow);

        var result = await _sut.GetOrFetchAsync("key", Bucket, Ttl,
            _ => Task.FromResult<string?>("recovered"));

        Assert.Equal("recovered", result);
    }

    [Fact]
    public async Task GetOrFetchAsync_FetchReturnsNull_ReturnsNullWithoutWritingFile_WhenNoCache()
    {
        var result = await _sut.GetOrFetchAsync<string>("key", Bucket, Ttl,
            _ => Task.FromResult<string?>(null), r => r != null);

        Assert.Null(result);
        Assert.False(File.Exists(CachePath("key")));
    }

    [Fact]
    public async Task GetOrFetchAsync_FetchReturnsNull_ReturnsNullAndWritesFile_WhenNoCacheGate()
    {
        var result = await _sut.GetOrFetchAsync<string>("key", Bucket, Ttl,
            _ => Task.FromResult<string?>(null));

        Assert.Null(result);
        Assert.True(File.Exists(CachePath("key")));
    }

    [Fact]
    public async Task GetOrFetchAsync_ShouldCacheReturnsFalse_ReturnsValueWithoutWritingFile()
    {
        var result = await _sut.GetOrFetchAsync(
            "key", Bucket, Ttl,
            _ => Task.FromResult<string?>("value"),
            shouldCache: _ => false);

        Assert.Equal("value", result);
        Assert.False(File.Exists(CachePath("key")));
    }

    [Fact]
    public async Task GetOrFetchAsync_KeySanitization_WritesToSanitizedPath()
    {
        await _sut.GetOrFetchAsync("Hello World/Test", Bucket, Ttl,
            _ => Task.FromResult<string?>("value"));

        // "Hello World/Test" → lowercase → replace [^a-z0-9_-] → "hello-world-test"
        Assert.True(File.Exists(CachePath("hello-world-test")));
    }

    [Fact]
    public async Task GetOrFetchAsync_ConcurrentCallsSameKey_FetchCalledOnce()
    {
        var fetchCount = 0;
        async Task<string?> Fetch(CancellationToken ct)
        {
            await Task.Delay(80, ct);
            Interlocked.Increment(ref fetchCount);
            return "value";
        }

        await Task.WhenAll(
            _sut.GetOrFetchAsync("key", Bucket, Ttl, Fetch),
            _sut.GetOrFetchAsync("key", Bucket, Ttl, Fetch));

        Assert.Equal(1, fetchCount);
    }

    [Fact]
    public async Task GetOrFetchAsync_CancellationToken_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _sut.GetOrFetchAsync("key", Bucket, Ttl,
                _ => Task.FromResult<string?>("value"), ct: cts.Token));
    }

    // ── Invalidate ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Invalidate_FileExists_DeletesFile()
    {
        var path = CachePath("key");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "\"value\"");

        _sut.Invalidate("key", Bucket);

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Invalidate_FileDoesNotExist_NoException()
    {
        var ex = Record.Exception(() => _sut.Invalidate("missing-key", Bucket));
        Assert.Null(ex);
    }

    // ── InvalidatePrefix ──────────────────────────────────────────────────────

    [Fact]
    public async Task InvalidatePrefix_MatchingFiles_DeletesAllAndLeavesNonMatching()
    {
        var dir = Path.Combine(_tempPath, Bucket);
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "series-1.json"), "\"a\"");
        await File.WriteAllTextAsync(Path.Combine(dir, "series-2.json"), "\"b\"");
        await File.WriteAllTextAsync(Path.Combine(dir, "volume-1.json"), "\"c\"");

        _sut.InvalidatePrefix("series", Bucket);

        Assert.False(File.Exists(Path.Combine(dir, "series-1.json")));
        Assert.False(File.Exists(Path.Combine(dir, "series-2.json")));
        Assert.True(File.Exists(Path.Combine(dir, "volume-1.json")));
    }

    [Fact]
    public async Task InvalidatePrefix_NoMatchingFiles_NoException()
    {
        var dir = Path.Combine(_tempPath, Bucket);
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "other-key.json"), "\"v\"");

        var ex = Record.Exception(() => _sut.InvalidatePrefix("nonexistent", Bucket));
        Assert.Null(ex);
    }

    [Fact]
    public void InvalidatePrefix_DirectoryDoesNotExist_NoException()
    {
        var ex = Record.Exception(() => _sut.InvalidatePrefix("any", "missing-bucket"));
        Assert.Null(ex);
    }
}
