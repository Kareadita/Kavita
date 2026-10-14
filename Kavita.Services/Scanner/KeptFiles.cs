using System;
using System.Collections.Generic;
using System.Linq;
using Kavita.Common.Extensions;
using Kavita.Models.Parser;

namespace Kavita.Services.Scanner;

/// <summary>
/// Files of a series this scan did not read: in folders skipped as unchanged, or skipped in a read folder because they
/// match the database. Nothing about them was looked at, so their entities are kept as they are
/// </summary>
internal sealed class KeptFiles
{
    /// <param name="Path">Normalized folder</param>
    /// <param name="IsShallow">Covers only files directly in the folder</param>
    private readonly record struct UnchangedFolder(string Path, bool IsShallow)
    {
        public bool Contains(string filePath)
        {
            var prefix = Path.TrimEnd('/') + '/';
            if (!filePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;

            return !IsShallow || filePath.IndexOf('/', prefix.Length) < 0;
        }
    }

    private readonly List<UnchangedFolder> _folders;
    private readonly HashSet<string> _files;

    private KeptFiles(List<UnchangedFolder> folders, HashSet<string> files)
    {
        _folders = folders;
        _files = files;
    }

    public static KeptFiles None { get; } = new([], []);

    public static KeptFiles From(IEnumerable<ParserInfo> placeholders)
    {
        var list = placeholders.Where(info => info.IsPlaceholder).ToList();
        var folders = list
            .Where(info => !string.IsNullOrEmpty(info.UnchangedFolderPath))
            .Select(info => new UnchangedFolder(info.UnchangedFolderPath, info.UnchangedFolderIsShallow))
            .Distinct()
            .ToList();

        return new KeptFiles(folders, [.. list.SelectMany(info => info.UnchangedFiles)]);
    }

    public bool IsEmpty => _folders.Count == 0 && _files.Count == 0;

    /// <param name="filePath">As stored on the file</param>
    public bool Contains(string filePath)
    {
        // Kept paths come from the folder map, which normalizes; a missed match would delete the file's chapter
        var normalized = filePath.NormalizePath();
        return _files.Contains(normalized) || _folders.Exists(folder => folder.Contains(normalized));
    }
}
