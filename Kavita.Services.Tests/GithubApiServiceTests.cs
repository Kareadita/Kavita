using Flurl.Http.Testing;
using Kavita.API.Services;
using Kavita.Models.Misc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Kavita.Services.Tests;

public class GithubApiServiceTests : IDisposable
{
    private const string ReleasesPath = "repos/test/repo/releases";

    private readonly string _tempPath;
    private readonly HttpTest _httpTest = new();
    private readonly GithubApiService _sut;

    public GithubApiServiceTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempPath);

        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.LongTermCacheDirectory.Returns(_tempPath);

        var fileCacheService = new FileCacheService(directoryService, Substitute.For<ILogger<FileCacheService>>());
        _sut = new GithubApiService(fileCacheService, new MemoryCache(new MemoryCacheOptions()),
            Substitute.For<ILogger<GithubApiService>>());
    }

    public void Dispose()
    {
        _httpTest.Dispose();
        if (Directory.Exists(_tempPath))
        {
            Directory.Delete(_tempPath, true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetAsync_ServesStaleCache_WhenGithubFails()
    {
        var cachedAt = DateTime.UtcNow.AddHours(-3);
        await WriteCache(ReleasesPath, new List<Item> { new() { Name = "v0.7.0" } }, cachedAt);
        _httpTest.RespondWith("{\"message\":\"API rate limit exceeded\"}", 403, RateLimitExhaustedHeaders());

        var result = await _sut.GetAsync<List<Item>>(ReleasesPath, TimeSpan.FromHours(1));

        Assert.Equal(CacheSource.StaleCache, result.Source);
        Assert.True(result.HasValue);
        Assert.Equal("v0.7.0", Assert.Single(result.Value).Name);
        Assert.Equal(cachedAt, result.FetchedAtUtc!.Value, TimeSpan.FromSeconds(1));
        Assert.NotNull(result.RetryAtUtc);
    }

    [Fact]
    public async Task GetAsync_SkipsGithub_WhilePaused()
    {
        _httpTest.RespondWith("{\"message\":\"API rate limit exceeded\"}", 403, RateLimitExhaustedHeaders());

        var first = await _sut.GetAsync<List<Item>>(ReleasesPath, TimeSpan.FromHours(1));
        var second = await _sut.GetAsync<List<Item>>("repos/test/repo/commits", TimeSpan.FromHours(1), forceRefresh: true);

        Assert.Single(_httpTest.CallLog);
        Assert.Equal(CacheSource.Unavailable, first.Source);
        Assert.Equal(CacheSource.Unavailable, second.Source);
        Assert.NotNull(second.RetryAtUtc);
    }

    private async Task WriteCache<T>(string apiPath, T value, DateTime writtenAtUtc)
    {
        var dir = Path.Combine(_tempPath, GithubApiService.CacheBucket);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{apiPath.Replace('/', '-')}.json");
        await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(value));
        File.SetLastWriteTimeUtc(path, writtenAtUtc);
    }

    private static Dictionary<string, string> RateLimitExhaustedHeaders()
    {
        return new Dictionary<string, string>
        {
            { "X-RateLimit-Limit", "60" },
            { "X-RateLimit-Remaining", "0" },
            { "X-RateLimit-Reset", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString() }
        };
    }

    public sealed class Item
    {
        public string Name { get; set; } = string.Empty;
    }
}
