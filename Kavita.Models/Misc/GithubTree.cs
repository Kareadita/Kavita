using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Kavita.Models.Misc;
#nullable enable

/// <summary>
/// Response of the GitHub git trees API, trimmed to the fields Kavita reads so the cached copy stays small
/// </summary>
public sealed class GithubTree
{
    [JsonPropertyName("tree")]
    public List<Entry> Tree { get; set; } = [];

    /// <summary>
    /// Folders and files directly inside folderPath, "" for the repo root
    /// </summary>
    public IEnumerable<Entry> GetChildren(string folderPath)
    {
        return Tree.Where(e => e.ParentPath == folderPath);
    }

    public Entry? Find(string path)
    {
        return Tree.FirstOrDefault(e => e.Path == path);
    }

    public sealed class Entry
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
        [JsonPropertyName("sha")]
        public string Sha { get; set; } = string.Empty;
        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonIgnore]
        public bool IsFolder => Type == "tree";
        [JsonIgnore]
        public bool IsFile => Type == "blob";
        [JsonIgnore]
        public string Name => Path[(Path.LastIndexOf('/') + 1)..];
        [JsonIgnore]
        public string ParentPath => Path.LastIndexOf('/') is var lastSlash and >= 0 ? Path[..lastSlash] : string.Empty;
    }
}
