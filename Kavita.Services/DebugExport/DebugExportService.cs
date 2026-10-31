using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Models.DebugExport;
using Kavita.Models.DTOs.SignalR;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.DebugExport;

public partial class DebugExportService(
    ILogger<DebugExportService> logger,
    IDirectoryService directoryService,
    IBackupService backupService,
    IExportDatabaseScrubService databaseScrubService,
    IExportSystemInfoService systemInfoService,
    IExportLogService logService,
    IEventHub eventHub) : IDebugExportService
{
    public const string ZipName = "debug-export.zip";
    private const string PartialZipName = ZipName + ".partial";
    private const string DatabaseName = "kavita.db";
    private const string SystemInfoName = "system-info.json";

    /// <inheritdoc />
    public async Task ExportAsync(CancellationToken ct = default)
    {
        var fileSystem = directoryService.FileSystem;
        var exportId = NewExportId();
        var folder = fileSystem.Path.Join(directoryService.DebugExportDirectory, exportId);
        logger.LogInformation("[DebugExport] Starting export {ExportId}", exportId);

        try
        {
            fileSystem.Directory.CreateDirectory(folder);
            var databasePath = fileSystem.Path.Join(folder, DatabaseName);

            await SendProgress(0f, "Copying database", ct);
            await backupService.SnapshotDatabase(databasePath, ct);

            await SendProgress(0.2f, "Scrubbing database", ct);
            var result = await databaseScrubService.ScrubAsync(databasePath, ct);

            await SendProgress(0.5f, "Reading system info", ct);
            var systemInfo = await systemInfoService.BuildAsync(result, folder, ct);

            // After BuildAsync: the scrubber copies the junk's replacements when built, and a mount host is only added there
            var scrubber = new ExportTextScrubber(result.Junk, result.Usernames);

            await SendProgress(0.7f, "Writing zip", ct);
            var bytes = await WriteZipAsync(folder, databasePath, systemInfo, scrubber, ct);

            logger.LogInformation("[DebugExport] Export {ExportId} finished, {Bytes} bytes", exportId, bytes);
            await SendProgress(1f, "Export finished", ct);
            await eventHub.SendMessageAsync(MessageFactory.Info, MessageFactory.DebugExportReadyEvent(exportId, bytes), ct: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[DebugExport] Export {ExportId} failed", exportId);

            DeleteFolder(folder);

            await SendProgress(1f, "Export failed", CancellationToken.None);
            await eventHub.SendMessageAsync(MessageFactory.Error, MessageFactory.DebugExportFailedEvent(exportId, ex.Message),
                ct: CancellationToken.None);
        }
    }

    /// <inheritdoc />
    public string? GetZipPath(string exportId)
    {
        if (!ExportIdRegex().IsMatch(exportId)) return null;

        var fileSystem = directoryService.FileSystem;
        var path = fileSystem.Path.Join(directoryService.DebugExportDirectory, exportId, ZipName);

        return fileSystem.File.Exists(path) ? path : null;
    }

    private async Task<long> WriteZipAsync(string folder, string databasePath, ExportSystemInfo systemInfo,
        ExportTextScrubber scrubber, CancellationToken ct)
    {
        var fileSystem = directoryService.FileSystem;
        var partialPath = fileSystem.Path.Join(folder, PartialZipName);

        await using (var stream = fileSystem.File.Create(partialPath))
        await using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            await using (var database = fileSystem.File.OpenRead(databasePath))
            await using (var entry = await zip.CreateEntry(DatabaseName, CompressionLevel.Optimal).OpenAsync(ct))
            {
                await database.CopyToAsync(entry, ct);
            }

            await logService.WriteLogsAsync(zip, scrubber, ct);

            await using (var entry = await zip.CreateEntry(SystemInfoName, CompressionLevel.Optimal).OpenAsync(ct))
            await using (var writer = new StreamWriter(entry))
            {
                await writer.WriteAsync(systemInfo.ToJson().AsMemory(), ct);
            }
        }

        var zipPath = fileSystem.Path.Join(folder, ZipName);
        fileSystem.File.Move(partialPath, zipPath);

        foreach (var file in fileSystem.Directory.EnumerateFiles(folder).Where(f => f != zipPath).ToList())
        {
            fileSystem.File.Delete(file);
        }

        return fileSystem.FileInfo.New(zipPath).Length;
    }

    private void DeleteFolder(string folder)
    {
        try
        {
            if (directoryService.FileSystem.Directory.Exists(folder))
            {
                directoryService.FileSystem.Directory.Delete(folder, true);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[DebugExport] Could not delete {Folder}, the nightly cleanup removes it after 24 hours", folder);
        }
    }

    private async Task SendProgress(float progress, string subtitle, CancellationToken ct)
    {
        await eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.DebugExportProgressEvent(progress, subtitle), ct: ct);
    }

    private static string NewExportId() =>
        $"{DateTime.Now.ToString("yyyyMMdd-hhmmtt", CultureInfo.InvariantCulture)}-{RandomNumberGenerator.GetHexString(4, lowercase: true)}";

    /// <example><c>20261031-0102PM-a3f9</c>: local time, then 4 random hex characters</example>
    [GeneratedRegex(@"^\d{8}-\d{4}[AP]M-[0-9a-f]{4}\z")]
    private static partial Regex ExportIdRegex();
}
