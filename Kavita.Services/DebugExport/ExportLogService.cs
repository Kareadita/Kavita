using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Models.DebugExport;

namespace Kavita.Services.DebugExport;

public class ExportLogService(IBackupService backupService, IDirectoryService directoryService) : IExportLogService
{
    private const int LogFileCount = 5;

    /// <inheritdoc />
    public async Task WriteLogsAsync(ZipArchive zip, ExportTextScrubber scrubber, CancellationToken ct = default)
    {
        var fileSystem = directoryService.FileSystem;
        // Serilog names each day's file kavita<yyyyMMdd>.log, so the newest sort last
        var files = backupService.GetLogFiles()
            .OrderByDescending(f => fileSystem.Path.GetFileName(f), StringComparer.Ordinal)
            .Take(LogFileCount);

        foreach (var file in files)
        {
            var entry = zip.CreateEntry($"logs/{fileSystem.Path.GetFileName(file)}", CompressionLevel.Optimal);

            // Serilog holds today's file open for writing (shared: true), so it must be opened shared too
            await using var source = fileSystem.File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(source);
            await using var target = await entry.OpenAsync(ct);
            await using var writer = new StreamWriter(target);

            while (await reader.ReadLineAsync(ct) is { } line)
            {
                await writer.WriteLineAsync(scrubber.Scrub(line).AsMemory(), ct);
            }
        }
    }
}
