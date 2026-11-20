using System;
using System.Linq;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.API.Services.ReadingLists;
using Kavita.Common;
using Kavita.Models.DTOs.ReadingLists.CBL;
using Kavita.Models.Misc;


namespace Kavita.Services.ReadingLists;

public class CblGithubService : ICblGithubService
{
    private const string Repo = "DieselTech/CBL-ReadingLists";
    private const string Branch = "main";
    private const string CblExtension = ".cbl";
    private const string Cblv2Extension = ".json";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(4);

    private readonly IGithubApiService _githubApiService;

    public CblGithubService(IGithubApiService githubApiService)
    {
        _githubApiService = githubApiService;
    }

    public async Task<CblRepoBrowseResultDto> BrowseRepo(string path = "")
    {
        var normalizedPath = NormalizePath(path);

        var tree = await _githubApiService.GetTreeAsync(Repo, Branch, CacheTtl);
        if (!tree.HasValue) throw new KavitaException("github-unavailable");

        if (normalizedPath != string.Empty && tree.Value.Find(normalizedPath) is not {IsFolder: true})
        {
            throw new KavitaException("cbl-repo-path-not-found");
        }

        var items = tree.Value.GetChildren(normalizedPath)
            .Select(ToItemDto)
            .Where(IsListed)
            .OrderBy(i => !i.IsDirectory)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CblRepoBrowseResultDto
        {
            Items = items,
            RateLimit = tree.RateLimit,
            FromCache = tree.Source != CacheSource.Live
        };
    }

    public async Task<string> GetFileSha(string filePath)
    {
        var normalizedPath = NormalizePath(filePath);

        var tree = await _githubApiService.GetTreeAsync(Repo, Branch, CacheTtl);
        if (!tree.HasValue) throw new KavitaException("github-unavailable");

        if (tree.Value.Find(normalizedPath) is not {IsFile: true} entry) throw new KavitaException("cbl-repo-path-not-found");

        return entry.Sha;
    }

    public async Task<CblRepoItemDto> GetFile(string filePath)
    {
        var tree = await _githubApiService.GetTreeAsync(Repo, Branch, CacheTtl);
        if (!tree.HasValue) throw new KavitaException("github-unavailable");

        if (tree.Value.Find(NormalizePath(filePath)) is not {IsFile: true} entry) throw new KavitaException("cbl-repo-path-not-found");

        var item = ToItemDto(entry);
        if (!IsListed(item)) throw new KavitaException("cbl-repo-path-not-found");

        return item;
    }

    public async Task<string> GetFileContent(string filePath)
    {
        var file = await GetFile(filePath);

        var content = await _githubApiService.DownloadRawAsync(Repo, Branch, file.Path);
        if (!content.HasValue) throw new KavitaException("cbl-repo-download-failed");

        return content.Value;
    }

    private CblRepoItemDto ToItemDto(GithubTree.Entry entry)
    {
        return new CblRepoItemDto
        {
            Name = entry.Name,
            Path = entry.Path,
            IsDirectory = entry.IsFolder,
            Sha = entry.Sha,
            Size = entry.Size,
            DownloadUrl = entry.IsFile ? _githubApiService.GetRawUrl(Repo, Branch, entry.Path) : null
        };
    }

    private static bool IsListed(CblRepoItemDto item)
    {
        // Insure .github or other meta files/directories are excluded
        if (item.Name.StartsWith('.')) return false;

        return item.IsDirectory
               || item.Name.EndsWith(CblExtension, StringComparison.OrdinalIgnoreCase)
               || item.Name.EndsWith(Cblv2Extension, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return path.Trim('/').Trim();
    }
}
