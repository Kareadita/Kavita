using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Common;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Microsoft.AspNetCore.StaticFiles;
using MimeTypes;

namespace Kavita.Services;

public class DownloadService(
    ICacheService cacheService,
    ILocalizationService localizationService,
    IDirectoryService directoryService
    ) : IDownloadService
{
    private static readonly FileExtensionContentTypeProvider FileTypeProvider = new();

    /// <summary>
    /// Downloads the first file in the file enumerable for download
    /// </summary>
    /// <param name="files"></param>
    /// <returns></returns>
    public Tuple<string, string, string> GetFirstFileDownload(IEnumerable<MangaFile> files)
    {
        var firstFile = files.Select(c => c.FilePath).First();
        return Tuple.Create(firstFile, GetContentTypeFromFile(firstFile), Path.GetFileName(firstFile));
    }

    /// <summary>
    /// Returns MIME type for the given path (ie application/x-cbz, image/jpeg)
    /// </summary>
    /// <param name="filepath">Doesn't need to exist on disk</param>
    /// <returns></returns>
    public static string GetContentTypeFromFile(string filepath)
    {
        // Figures out what the content type should be based on the file name.
        if (!FileTypeProvider.TryGetContentType(filepath, out var contentType))
        {
            contentType ??= Path.GetExtension(filepath);

            contentType = Path.GetExtension(filepath).ToLowerInvariant() switch
            {
                ".cbz" => "application/x-cbz",
                ".cbr" => "application/x-cbr",
                ".cb7" => "application/x-cb7",
                ".cbt" => "application/x-cbt",
                ".epub" => "application/epub+zip",
                ".7z" => "application/x-7z-compressed",
                ".7zip" => "application/x-7z-compressed",
                ".rar" => "application/vnd.rar",
                ".zip" => "application/zip",
                ".tar.gz" => "application/gzip",
                ".pdf" => "application/pdf",
                _ => MimeTypeMap.GetMimeType(contentType)
            };
        }

        return contentType;
    }

    public async Task<string> DownloadChapter(Chapter chapter, CancellationToken cancellationToken)
    {
        if (chapter.Files is null)
        {
            throw new ArgumentNullException(nameof(chapter.Files));
        }

        if (chapter.Files.Count == 0)
        {
            return string.Empty;
        }

        return await CollectFiles(chapter, cancellationToken);
    }

    private async Task<string> CollectFiles(Chapter chapter, CancellationToken cancellationToken)
    {
        if (chapter.Files.Count <= 1)
        {
            return chapter.Files.First().FilePath;
        }

        var format = chapter.Files.First().Format;
        if (format is not MangaFormat.Archive && format is not MangaFormat.Image)
        {
            // We currently do not support downloading multipart non Archive files
            throw new NotSupportedException();
        }

        if (await cacheService.Ensure(chapter.Id, ct: cancellationToken) is null)
        {
            throw new KavitaException(await localizationService.TranslateAsync("cache-file-find"));
        }

        var dateStr = DateTime.UtcNow.ToShortDateString().Replace("/", "_");
        var outputPath = Path.Join(directoryService.TempDirectory, $"kavita_merged_c{chapter.Id}_{dateStr}.cbz");

        if (!File.Exists(outputPath))
        {
            var cacheDir = cacheService.GetCachePath(chapter.Id);
            await ZipFile.CreateFromDirectoryAsync(cacheDir, outputPath, cancellationToken);

        }

        return outputPath;
    }
}
