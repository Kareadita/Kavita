using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Plus;
using Kavita.API.Services.Scanner;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Kavita.Models.DebugExport;
using Kavita.Models.DTOs.KavitaPlus.License;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Scanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.DebugExport;

public partial class ExportSystemInfoService(
    ILogger<ExportSystemInfoService> logger,
    IDataContext context,
    IDirectoryService directoryService,
    IMountDetectorService mountDetector,
    ILicenseService licenseService) : IExportSystemInfoService
{
    private const int ReadFilesPerLibrary = 3;
    private const int ReadBytesCap = 32 * 1024 * 1024;
    private const int WriteRounds = 20;
    private const int WriteBytesEach = 4096;

    /// <inheritdoc />
    public async Task<ExportSystemInfo> BuildAsync(ExportScrubResult result, string exportFolder, CancellationToken ct = default)
    {
        var junk = result.Junk;
        var configDirectory = directoryService.ConfigDirectory;

        return new ExportSystemInfo(
            new ExportSystemInfo.ExportSection(DateTime.UtcNow, BuildInfo.Version.ToString(), result.AdminUserName, result.AdminPassword, result.AdminAuthKey),
            new ExportSystemInfo.RuntimeSection(
                RuntimeInformation.OSDescription,
                RuntimeInformation.OSArchitecture.ToString(),
                RuntimeInformation.FrameworkDescription,
                RuntimeInformation.RuntimeIdentifier,
                OsInfo.IsDocker,
                Environment.ProcessorCount,
                CultureInfo.CurrentCulture.EnglishName),
            new ExportSystemInfo.ConfigSection(configDirectory, ToMount(DetectMount(configDirectory), junk, withFingerprint: false), await TimeWritesAsync(exportFolder, ct)),
            await GetLibrariesAsync(junk, ct),
            await GetKavitaPlusAsync(ct));
    }

    private async Task<List<ExportSystemInfo.LibrarySection>> GetLibrariesAsync(ExportJunk junk, CancellationToken ct)
    {
        var libraries = await context.Library
            .AsNoTracking()
            .OrderBy(l => l.Id)
            .Select(l => new { l.Id, Folders = l.Folders.Select(f => f.Path).ToList() })
            .ToListAsync(ct);

        var sections = new List<ExportSystemInfo.LibrarySection>();
        foreach (var library in libraries)
        {
            var folders = library.Folders
                .Select(path => new ExportSystemInfo.FolderSection(path, ToMount(DetectMount(path), junk, withFingerprint: true)))
                .ToList();

            var files = await context.MangaFile
                .AsNoTracking()
                .Where(f => f.Chapter.Volume.Series.LibraryId == library.Id)
                .OrderBy(_ => EF.Functions.Random())
                .Take(ReadFilesPerLibrary)
                .Select(f => new { f.Id, f.FilePath })
                .ToListAsync(ct);

            var readTimes = new List<ExportSystemInfo.ReadTimeSection>();
            foreach (var file in files)
            {
                readTimes.Add(await TimeReadsAsync(file.Id, file.FilePath, ct));
            }

            sections.Add(new ExportSystemInfo.LibrarySection(library.Id, folders, readTimes));
        }

        return sections;
    }

    private async Task<ExportSystemInfo.KavitaPlusSection> GetKavitaPlusAsync(CancellationToken ct)
    {
        var licenseKey = await context.ServerSetting
            .Where(s => s.Key == ServerSettingKey.LicenseKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(licenseKey))
        {
            return new ExportSystemInfo.KavitaPlusSection(false, null, HashUtil.ServerToken());
        }

        LicenseInfoDto? info;
        try
        {
            // forceCheck: false, since a fetch also caches and sends LicenseInfoUpdate to admins
            info = await licenseService.GetLicenseInfo(false, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "[DebugExport] License lookup failed, recorded as unavailable");
            info = null;
        }

        object license = info == null
            ? ExportSystemInfo.Unavailable
            : new ExportSystemInfo.LicenseSection(info.State, info.IsActive, info.ExpirationDate, info.PastDue, info.IsValidVersion);

        return new ExportSystemInfo.KavitaPlusSection(true, license, HashUtil.ServerToken());
    }

    private async Task<ExportSystemInfo.ReadTimeSection> TimeReadsAsync(int fileId, string path, CancellationToken ct)
    {
        try
        {
            var buffer = new byte[1024 * 1024];
            var first = await ReadAsync(path, buffer, ct);
            var second = await ReadAsync(path, buffer, ct);

            return new ExportSystemInfo.ReadTimeSection(fileId, first.Bytes, first.Ms, second.Ms, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ExportSystemInfo.ReadTimeSection(fileId, null, null, null, ex.GetType().Name);
        }
    }

    private async Task<ReadResult> ReadAsync(string path, byte[] buffer, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        await using var stream = directoryService.FileSystem.File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        long total = 0;
        while (total < ReadBytesCap)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, ReadBytesCap - total)), ct);
            if (read == 0) break;

            total += read;
        }

        return new ReadResult(total, sw.ElapsedMilliseconds);
    }

    private async Task<ExportSystemInfo.WriteTimesSection> TimeWritesAsync(string exportFolder, CancellationToken ct)
    {
        var fileSystem = directoryService.FileSystem;
        var path = fileSystem.Path.Join(exportFolder, "write-timing.tmp");
        try
        {
            var buffer = new byte[WriteBytesEach];
            var times = new List<double>(WriteRounds);
            await using (var stream = fileSystem.File.Open(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                for (var round = 0; round < WriteRounds; round++)
                {
                    var sw = Stopwatch.StartNew();
                    await stream.WriteAsync(buffer, ct);
                    // flushToDisk: true is the fsync a SQLite commit waits on, a plain Flush stops at the OS cache
                    stream.Flush(true);
                    times.Add(sw.Elapsed.TotalMilliseconds);
                }
            }

            times.Sort();
            return new ExportSystemInfo.WriteTimesSection(WriteRounds, WriteBytesEach, Math.Round(times[times.Count / 2], 2), Math.Round(times[^1], 2), null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ExportSystemInfo.WriteTimesSection(WriteRounds, WriteBytesEach, null, null, ex.GetType().Name);
        }
        finally
        {
            if (fileSystem.File.Exists(path))
            {
                fileSystem.File.Delete(path);
            }
        }
    }

    private MountInfo? DetectMount(string folder)
    {
        try
        {
            return mountDetector.Detect(folder);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[DebugExport] Mount lookup failed for {Folder}, left out", folder);
            return null;
        }
    }

    private static ExportSystemInfo.MountSection? ToMount(MountInfo? mount, ExportJunk junk, bool withFingerprint)
    {
        if (mount == null) return null;

        var fingerprint = withFingerprint ? junk.Replace(ExportScrubKind.Fingerprint, mount.Fingerprint) : null;
        return new ExportSystemInfo.MountSection(mount.Point, ScrubSource(mount.Source, junk), mount.Type, mount.IsNetwork, fingerprint);
    }

    /// <summary>
    /// ExportJunk's host parser needs the host first, so <c>//host/share</c> and <c>host:/path</c> are split here
    /// </summary>
    private static string ScrubSource(string source, ExportJunk junk)
    {
        var match = SharePrefixRegex().Match(source);
        if (!match.Success)
        {
            match = NfsPrefixRegex().Match(source);
        }

        if (!match.Success) return source;

        var host = match.Groups["host"];
        return source[..host.Index] + junk.Replace(ExportScrubKind.Host, host.Value) + source[(host.Index + host.Length)..];
    }

    /// <example><c>//192.168.1.5/books</c> has host <c>192.168.1.5</c></example>
    [GeneratedRegex(@"^(//|\\\\)(?<host>[^/\\]+)")]
    private static partial Regex SharePrefixRegex();

    /// <example><c>nas.home.lan:/export/books</c> has host <c>nas.home.lan</c>. Two characters at least, so a drive like <c>C:/</c> is not a host</example>
    [GeneratedRegex(@"^(?<host>[^/\\:]{2,}):/")]
    private static partial Regex NfsPrefixRegex();

    private readonly record struct ReadResult(long Bytes, long Ms);
}
