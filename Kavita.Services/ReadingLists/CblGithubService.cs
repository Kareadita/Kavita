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
            .Select(e => new CblRepoItemDto
            {
                Name = e.Name,
                Path = e.Path,
                IsDirectory = e.IsFolder,
                Sha = e.Sha,
                Size = e.Size,
                DownloadUrl = e.IsFile ? _githubApiService.GetRawUrl(Repo, Branch, e.Path) : null
            })
            .Where(i => !i.Name.StartsWith('.')) // Insure .github or other meta files/directories are excluded
            .Where(i => i.IsDirectory || i.Name.EndsWith(CblExtension, StringComparison.OrdinalIgnoreCase) || i.Name.EndsWith(Cblv2Extension, StringComparison.OrdinalIgnoreCase))
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

    public async Task<string> GetFileContent(string filePath)
    {
        var content = await _githubApiService.DownloadRawAsync(Repo, Branch, NormalizePath(filePath));
        if (!content.HasValue) throw new KavitaException("cbl-repo-download-failed");

        return content.Value;
    }

    private static string NormalizePath(string path)
    {
        return path.Trim('/').Trim();
    }
}
