using Flurl.Http.Testing;
using Kavita.API.Services;
using Kavita.Services.ReadingLists;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Kavita.Services.Tests.ReadingLists;

public class CblGithubServiceTests : IDisposable
{
    private const string Tree = """
        {
          "sha": "root",
          "tree": [
            { "path": ".github", "type": "tree", "sha": "t1" },
            { "path": ".github/workflow.yml", "type": "blob", "sha": "b0", "size": 10 },
            { "path": "README.md", "type": "blob", "sha": "b1", "size": 20 },
            { "path": "Marvel", "type": "tree", "sha": "t2" },
            { "path": "Marvel/Civil War.cbl", "type": "blob", "sha": "b2", "size": 300 },
            { "path": "Marvel/Events", "type": "tree", "sha": "t3" },
            { "path": "Marvel/Events/Secret Wars.cbl", "type": "blob", "sha": "b3", "size": 400 },
            { "path": "DC", "type": "tree", "sha": "t4" },
            { "path": "Top.cbl", "type": "blob", "sha": "b5", "size": 50 }
          ],
          "truncated": false
        }
        """;

    private readonly string _tempPath;
    private readonly HttpTest _httpTest = new();
    private readonly CblGithubService _sut;

    public CblGithubServiceTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempPath);

        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.LongTermCacheDirectory.Returns(_tempPath);

        var fileCacheService = new FileCacheService(directoryService, Substitute.For<ILogger<FileCacheService>>());
        var githubApiService = new GithubApiService(fileCacheService, new MemoryCache(new MemoryCacheOptions()),
            Substitute.For<ILogger<GithubApiService>>());
        _sut = new CblGithubService(githubApiService);
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
    public async Task BrowseRepo_ListsOnlyDirectChildren()
    {
        _httpTest.RespondWith(Tree);

        var root = await _sut.BrowseRepo();
        var marvel = await _sut.BrowseRepo("/Marvel/");

        Assert.Equal(["DC", "Marvel", "Top.cbl"], root.Items.Select(i => i.Name));
        Assert.False(root.FromCache);
        Assert.Equal(["Events", "Civil War.cbl"], marvel.Items.Select(i => i.Name));
        Assert.True(marvel.FromCache);

        var civilWar = marvel.Items[1];
        Assert.Equal("Marvel/Civil War.cbl", civilWar.Path);
        Assert.Equal("b2", civilWar.Sha);
        Assert.Equal(300, civilWar.Size);
        Assert.Equal("https://raw.githubusercontent.com/DieselTech/CBL-ReadingLists/main/Marvel/Civil%20War.cbl", civilWar.DownloadUrl);
        Assert.Null(marvel.Items[0].DownloadUrl);
    }

    [Fact]
    public async Task BrowseAndShaLookups_ShareOneGithubCall()
    {
        _httpTest.RespondWith(Tree);

        await _sut.BrowseRepo("Marvel");
        Assert.Equal("b2", await _sut.GetFileSha("Marvel/Civil War.cbl"));
        Assert.Equal("b3", await _sut.GetFileSha("Marvel/Events/Secret Wars.cbl"));
        Assert.Equal("b5", await _sut.GetFileSha("Top.cbl"));

        Assert.Single(_httpTest.CallLog);
    }
}
