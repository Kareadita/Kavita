using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
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
    private const string TreePath = $"repos/{Repo}/git/trees/{Branch}?recursive=1";
    private const string TreeType = "tree";
    private const string BlobType = "blob";
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

        var tree = await _githubApiService.GetAsync<GitTree>(TreePath, CacheTtl);
        if (!tree.HasValue) throw new KavitaException("github-unavailable");

        if (normalizedPath != string.Empty && !tree.Value.Tree.Any(e => e.Type == TreeType && e.Path == normalizedPath))
        {
            throw new KavitaException("cbl-repo-path-not-found");
        }

        var items = tree.Value.Tree
            .Where(e => GetParentPath(e.Path) == normalizedPath)
            .Select(e => new CblRepoItemDto
            {
                Name = GetName(e.Path),
                Path = e.Path,
                IsDirectory = e.Type == TreeType,
                Sha = e.Sha,
                Size = e.Size,
                DownloadUrl = e.Type == BlobType ? _githubApiService.GetRawUrl(Repo, Branch, e.Path) : null
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

        var tree = await _githubApiService.GetAsync<GitTree>(TreePath, CacheTtl);
        if (!tree.HasValue) throw new KavitaException("github-unavailable");

        var entry = tree.Value.Tree.FirstOrDefault(e => e.Type == BlobType && e.Path == normalizedPath);
        if (entry == null) throw new KavitaException("cbl-repo-path-not-found");

        return entry.Sha;
    }

    public async Task<string> GetFileContent(string filePath)
    {
        var content = await _githubApiService.DownloadRawAsync(Repo, Branch, NormalizePath(filePath));
        if (!content.HasValue) throw new KavitaException("cbl-repo-download-failed");

        return content.Value;
    }

    private static string GetParentPath(string path)
    {
        var lastSlash = path.LastIndexOf('/');
        return lastSlash < 0 ? string.Empty : path[..lastSlash];
    }

    private static string GetName(string path)
    {
        return path[(path.LastIndexOf('/') + 1)..];
    }

    private static string NormalizePath(string path)
    {
        return path.Trim('/').Trim();
    }

    /// <summary>
    /// Response of the git trees API, trimmed to the fields Kavita reads so the cached copy stays small
    /// </summary>
    private sealed class GitTree
    {
        [JsonPropertyName("tree")]
        public List<GitTreeEntry> Tree { get; set; } = [];
    }

    private sealed class GitTreeEntry
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
        [JsonPropertyName("sha")]
        public string Sha { get; set; } = string.Empty;
        [JsonPropertyName("size")]
        public long Size { get; set; }
    }
}
