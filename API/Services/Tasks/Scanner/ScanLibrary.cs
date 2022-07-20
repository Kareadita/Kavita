using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Entities;
using API.Helpers;
using API.Parser;
using Kavita.Common.Helpers;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks.Scanner;

/// <summary>
/// This is responsible for scanning and updating a Library
/// </summary>
public class ScanLibrary
{
    private readonly IDirectoryService _directoryService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger _logger;

    public ScanLibrary(IDirectoryService directoryService, IUnitOfWork unitOfWork, ILogger logger)
    {
        _directoryService = directoryService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }


    // public Task UpdateLibrary(Library library)
    // {
    //
    //
    // }




    /// <summary>
    /// Gets the list of all parserInfos given a Series (Will match on Name, LocalizedName, OriginalName). If the series does not exist within, return empty list.
    /// </summary>
    /// <param name="parsedSeries"></param>
    /// <param name="series"></param>
    /// <returns></returns>
    public static IList<ParserInfo> GetInfosByName(Dictionary<ParsedSeries, List<ParserInfo>> parsedSeries, Series series)
    {
        var allKeys = parsedSeries.Keys.Where(ps =>
            SeriesHelper.FindSeries(series, ps));

        var infos = new List<ParserInfo>();
        foreach (var key in allKeys)
        {
            infos.AddRange(parsedSeries[key]);
        }

        return infos;
    }


    /// <summary>
    /// This will Scan all files in a folder path. For each folder within the folderPath, FolderAction will be invoked for all files contained
    /// </summary>
    /// <param name="folderPath">A library folder or series folder</param>
    /// <param name="folderAction">A callback async Task to be called once all files for each folder path are found</param>
    public async Task ProcessFiles(string folderPath, bool isLibraryFolder, Func<IEnumerable<string>, string,Task> folderAction)
    {
        if (isLibraryFolder)
        {
            var directories = _directoryService.GetDirectories(folderPath).ToList();

            foreach (var directory in directories)
            {
                // For a scan, this is doing everything in the directory loop before the folder Action is called...which leads to no progress indication
                await folderAction(_directoryService.ScanFiles(directory), directory);
            }
        }
        else
        {
            //folderAction(ScanFiles(folderPath));
            await folderAction(_directoryService.ScanFiles(folderPath), folderPath);
        }
    }



        private GlobMatcher CreateIgnoreMatcher(string ignoreFile)
        {
            if (!_directoryService.FileSystem.File.Exists(ignoreFile))
            {
                return null;
            }

            // Read file in and add each line to Matcher
            var lines = _directoryService.FileSystem.File.ReadAllLines(ignoreFile);
            if (lines.Length == 0)
            {
                _logger.LogError("Kavita Ignore file found but empty, ignoring: {IgnoreFile}", ignoreFile);
                return null;
            }

            GlobMatcher matcher = new();
            foreach (var line in lines)
            {
                matcher.AddExclude(line);
            }

            return matcher;
        }
}
