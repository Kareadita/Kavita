using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Docnet.Core;
using Docnet.Core.Models;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Scanner;
using Kavita.API.Services.SignalR;
using Kavita.Models.DTOs.Archive;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Scanner;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;

namespace Kavita.Services.Scanner;

public class FolderValidationService(
    IUnitOfWork unitOfWork,
    IArchiveService archiveService,
    IDirectoryService directoryService,
    IEventHub eventHub,
    ILogger<FolderValidationService> logger)
    : IFolderValidationService
{
    // Covers a copy that opened its file shortly before the first change event reached Kavita
    private static readonly TimeSpan WrittenBeforeChange = TimeSpan.FromMinutes(2);

    public async Task ValidateFolder(ScanFolderRequest request, DateTime firstChangeUtc)
    {
        var delay = TaskScheduler.WatcherPushBack(firstChangeUtc, DateTime.UtcNow);
        if (delay <= TimeSpan.Zero)
        {
            logger.LogInformation("[FolderValidationService] Changes in {Folder} started {Minutes} minutes ago, scanning without validation",
                request.ChangedPath, TaskScheduler.WatcherScanCap.TotalMinutes);
            QueueScanFolder(request);
            return;
        }

        var stillWriting = FindFilesStillWriting(request.ChangedPath, !await IsLibraryRoot(request), firstChangeUtc);
        if (stillWriting.Count == 0)
        {
            QueueScanFolder(request);
            return;
        }

        logger.LogDebug("[FolderValidationService] {Count} file(s) in {Folder} still being written, checking again in {Seconds} s: {Files}",
            stillWriting.Count, request.ChangedPath, delay.TotalSeconds, string.Join(", ", stillWriting));
        if (!ScheduleValidation(request, firstChangeUtc, delay)) return;

        await eventHub.SendMessageAsync(MessageFactory.ScanRescheduled, TaskScheduler.ScanQueueChangedEvent());
    }

    public IList<string> FindFilesStillWriting(string folder, bool recursive, DateTime sinceUtc)
    {
        var directory = directoryService.FileSystem.DirectoryInfo.New(folder);
        if (!directory.Exists) return [];

        var options = new EnumerationOptions { RecurseSubdirectories = recursive, IgnoreInaccessible = true };
        var cutoff = sinceUtc - WrittenBeforeChange;

        return directory.EnumerateFiles("*", options)
            .Where(f => f.LastWriteTimeUtc >= cutoff)
            .Select(f => Parser.NormalizePath(f.FullName))
            .Where(path => Parser.ParseFormat(path) != MangaFormat.Unknown && !Parser.HasBlacklistedFolderInPath(path))
            .Where(IsStillWriting)
            .ToList();
    }

    private static void QueueScanFolder(ScanFolderRequest request)
    {
        lock (TaskScheduler.ScanRequestLock)
        {
            ScanJobQueue.Enqueue<IScannerService>(s => s.ScanFolder(request));
        }
    }

    /// <summary>
    /// A change event during this validation already scheduled one for the same request, which then covers this one
    /// </summary>
    /// <returns>False when the other validation already covers it</returns>
    private static bool ScheduleValidation(ScanFolderRequest request, DateTime firstChangeUtc, TimeSpan delay)
    {
        lock (TaskScheduler.ScanRequestLock)
        {
            if (ScanJobQueue.Read().FolderJobs.Any(j => j.Request == request && j.FirstChangeUtc != null && j.RunAtUtc != null)) return false;

            ScanJobQueue.Schedule<IFolderValidationService>(s => s.ValidateFolder(request, firstChangeUtc), delay);
            return true;
        }
    }

    private async Task<bool> IsLibraryRoot(ScanFolderRequest request)
    {
        var library = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(request.LibraryId, LibraryIncludes.Folders);
        return library != null && library.Folders.Any(f => Parser.NormalizePath(f.Path).TrimEnd('/') == request.ChangedPath.TrimEnd('/'));
    }

    private bool IsStillWriting(string path)
    {
        try
        {
            // On Windows a copy holds its file open for writing until the last byte is in, for every copy method
            using var denyWriters = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

            return Parser.ParseFormat(path) switch
            {
                MangaFormat.Archive or MangaFormat.Epub => !CanOpenArchive(path),
                MangaFormat.Pdf => !CanOpenPdf(path),
                _ => false,
            };
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (IOException ex)
        {
#pragma warning disable S6667
            logger.LogDebug("[FolderValidationService] {File} is open for writing by another process: {Message}", path, ex.Message);
#pragma warning restore S6667
            return true;
        }
    }

    private bool CanOpenArchive(string path)
    {
        return archiveService.CanOpen(path) switch
        {
            ArchiveLibrary.Default => true,
            // CanOpen trusts a rar by its extension, and SharpCompress opens a half-written zip; both fail once entries are read
            ArchiveLibrary.SharpCompress => CanReadEntries(path),
            _ => false,
        };
    }

    private bool CanReadEntries(string path)
    {
        try
        {
            using var archive = ArchiveFactory.OpenArchive(path);
            _ = archive.Entries.Count();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogDebug("[FolderValidationService] {File} cannot be opened yet: {Message}", path, ex.Message);
            return false;
        }
    }

    private bool CanOpenPdf(string path)
    {
        try
        {
            using var reader = DocLib.Instance.GetDocReader(path, new PageDimensions(1080, 1920));
            _ = reader.GetPageCount();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogDebug("[FolderValidationService] {File} cannot be opened yet: {Message}", path, ex.Message);
            return false;
        }
    }
}
