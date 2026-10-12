using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;

namespace Kavita.Models.Parser;
#nullable enable

/// <summary>
/// Every folder below a library root with the library files directly in it, from one listing per folder.
/// Paths are normalized
/// </summary>
public sealed class FolderTree(string root)
{
    private readonly List<string> _listOrder = [];
    private readonly Dictionary<string, Folder> _folders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _children = new(StringComparer.Ordinal);
    private List<string>? _longestFirst;

    public string Root { get; } = root;
    public int FileCount { get; private set; }
    public int UnreadableCount { get; private set; }

    /// <summary>
    /// Folders below the root, longest path first. Folders of equal length keep the order they were listed in
    /// </summary>
    public IReadOnlyList<string> Folders => _longestFirst ??= _listOrder.OrderByDescending(f => f.Length).ToList();

    public void Add(string folder, string parent, IReadOnlyList<FileStamp> files)
    {
        AddFolder(folder, parent, new Folder(files, null));
        FileCount += files.Count;
    }

    /// <summary>
    /// A folder that could not be listed. Reading its files rethrows <paramref name="error"/>
    /// </summary>
    public void AddUnreadable(string folder, string parent, Exception error)
    {
        AddFolder(folder, parent, new Folder([], ExceptionDispatchInfo.Capture(error)));
        UnreadableCount++;
    }

    /// <summary>
    /// The files directly in the folder
    /// </summary>
    public IReadOnlyList<FileStamp> FilesIn(string folder)
    {
        var entry = _folders[folder];
        entry.Error?.Throw();
        return entry.Files;
    }

    /// <summary>
    /// The files in the folder and every folder below it. Throws if any of them could not be listed
    /// </summary>
    public List<FileStamp> FilesBelow(string folder)
    {
        var files = new List<FileStamp>();
        var pending = new Stack<string>();
        pending.Push(folder);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            files.AddRange(FilesIn(current));
            if (!_children.TryGetValue(current, out var children)) continue;

            foreach (var child in children)
            {
                pending.Push(child);
            }
        }

        return files;
    }

    private void AddFolder(string folder, string parent, Folder entry)
    {
        _folders.Add(folder, entry);
        _listOrder.Add(folder);
        _longestFirst = null;

        if (!_children.TryGetValue(parent, out var siblings))
        {
            siblings = [];
            _children[parent] = siblings;
        }
        siblings.Add(folder);
    }

    private sealed record Folder(IReadOnlyList<FileStamp> Files, ExceptionDispatchInfo? Error);
}
