using System;
using System.Collections.Generic;
using System.Linq;
using Kavita.Common.Extensions;
using Kavita.Models.Parser;

namespace Kavita.Services.Scanner;

/// <summary>
/// What the DB knows about the series in each folder. The walk asks it two questions:
/// <list type="number">
/// <item>Can I skip reading this folder, or a file in it? Change detection, always "no" on a forced scan
/// (<see cref="IsUnchanged"/>, <see cref="OwnersToCompareIn"/>, <see cref="CanCompare"/>, <see cref="FilesIn"/>)</item>
/// <item>Who lives in this folder, so they are not deleted when it can't be read? Force does not change the answer
/// (<see cref="OwnersIn"/>, <see cref="OwnersInside"/>)</item>
/// </list>
/// </summary>
public sealed class KnownSeriesFolders
{
    public readonly record struct OwnedFile(KnownFile File, SeriesModified Owner);

    private readonly IDictionary<string, IList<SeriesModified>> _byFolder;
    private readonly bool _forced;

    /// <param name="byFolder">Normalized folder to the series with files in it</param>
    /// <param name="forced">Bypass change detection and read every folder</param>
    public KnownSeriesFolders(IDictionary<string, IList<SeriesModified>> byFolder, bool forced)
    {
        _byFolder = byFolder;
        _forced = forced;
    }

    /// <summary>
    /// How many folders the DB knows series in. Logged in the Step 1 Debug line
    /// </summary>
    public int FolderCount => _byFolder.Count;

    /// <summary>
    /// Every known series, once each
    /// </summary>
    public IEnumerable<SeriesModified> AllSeries => _byFolder.Values.SelectMany(s => s).Distinct();

    /// <summary>
    /// The folder can be skipped: its listing matches what the owners and failed files say is there. Always false when forced
    /// </summary>
    public bool IsUnchanged(IList<FileStamp> onDisk, List<SeriesModified> owners, List<FailedFile> failedFiles,
        Func<string, bool> isInScope, IDictionary<int, DateTime> writeTimesToBackfill)
    {
        return !_forced && (owners.Count > 0 || failedFiles.Count > 0) &&
               FolderChangeCheck.IsUnchanged(onDisk, owners, failedFiles, isInScope, writeTimesToBackfill);
    }

    /// <summary>
    /// Series with files directly in the folder, to check the listing against for change detection.
    /// Empty when forced, so nothing is skipped. To keep series in a folder that can't be read, use <see cref="OwnersIn"/>
    /// </summary>
    public List<SeriesModified> OwnersToCompareIn(string folder)
    {
        return _forced ? [] : OwnersIn(folder);
    }

    /// <summary>
    /// Every series keyed at the folder has a usable <see cref="SeriesModified.LowestFolderPath"/>, so the folder can be
    /// checked for changes as one unit. False when forced
    /// </summary>
    /// <param name="folder">This should be normalized</param>
    public bool CanCompare(string folder)
    {
        if (_forced || !_byFolder.TryGetValue(folder, out var seriesList))
        {
            return false;
        }

        // Null stays "changed" (FolderPath alone misses a deleted sibling folder)
        return seriesList.All(series => !string.IsNullOrEmpty(series.LowestFolderPath) &&
                                        series.LibraryRoots.Any(series.LowestFolderPath.IsInsideFolder));
    }

    /// <summary>
    /// Every series keyed at the folder, including ones whose files are all in subfolders
    /// </summary>
    public IList<SeriesModified> SeriesAt(string folder)
    {
        return _byFolder.TryGetValue(folder, out var seriesList) ? seriesList : [];
    }

    /// <summary>
    /// Series with files directly in the folder. Ignores force
    /// </summary>
    public List<SeriesModified> OwnersIn(string folder)
    {
        return _byFolder.TryGetValue(folder, out var seriesList)
            ? seriesList.Where(s => s.FilesByFolder.ContainsKey(folder)).ToList()
            : [];
    }

    /// <summary>
    /// The stored file and its series for each file directly in the folders, so a changed folder can skip its unchanged files.
    /// Empty when forced, so every file is read
    /// </summary>
    /// <param name="folders">These should be normalized</param>
    public Dictionary<string, OwnedFile> FilesIn(IEnumerable<string> folders)
    {
        var files = new Dictionary<string, OwnedFile>();
        if (_forced) return files;

        foreach (var folder in folders)
        {
            foreach (var owner in OwnersIn(folder))
            {
                foreach (var file in owner.FilesByFolder[folder])
                {
                    files.TryAdd(file.Path, new OwnedFile(file, owner));
                }
            }
        }

        return files;
    }

    /// <summary>
    /// Series with files in the folder or any folder below it. Ignores force
    /// </summary>
    public List<SeriesModified> OwnersInside(string folder)
    {
        return AllSeries
            .Where(s => s.FilesByFolder.Keys.Any(f => f.IsSameOrInsideFolder(folder)))
            .ToList();
    }
}
