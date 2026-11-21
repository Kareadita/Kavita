using System.Collections.Generic;
using System.Threading.Tasks;
using Kavita.Models.DTOs.ReadingLists.CBL;

namespace Kavita.API.Services.ReadingLists;

public interface ICblGithubService
{
    /// <summary>
    /// Browse a directory in the CBL repo. Returns folders and .cbl/.json files only.
    /// Served from the repo tree, which is fetched once and cached.
    /// </summary>
    Task<CblRepoBrowseResultDto> BrowseRepo(string path = "");
    /// <summary>
    /// Returns the Git blob SHA for a file, from the cached repo tree
    /// </summary>
    Task<string> GetFileSha(string filePath);
    /// <summary>
    /// The listed .cbl/.json file at this repo path, from the cached repo tree
    /// </summary>
    /// <exception cref="Kavita.Common.KavitaException">cbl-repo-path-not-found when the path is not a listed file in the tree</exception>
    Task<CblRepoItemDto> GetFile(string filePath);
    /// <summary>
    /// Downloads the raw content of a listed .cbl/.json file by its repo path. Paths outside the repo tree are refused
    /// </summary>
    Task<string> GetFileContent(string filePath);
}
