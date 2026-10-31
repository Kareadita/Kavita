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
    IDirectoryService directoryService,
    IKepubifyConverterService kepubifyConverterService
    ) : IDownloadService
{
    private readonly FileExtensionContentTypeProvider _fileTypeProvider = new();

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

    public string GetContentTypeFromFile(string filepath)
    {
        // Figures out what the content type should be based on the file name.
        if (!_fileTypeProvider.TryGetContentType(filepath, out var contentType))
        {
            if (contentType == null)
            {
                // Get extension
                contentType = Path.GetExtension(filepath);
            }

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

        return contentType!;
    }

    public async Task<string> DownloadChapter(Chapter chapter, DownloadFormat format, CancellationToken cancellationToken)
    {
        if (chapter.Files is null)
        {
            throw new ArgumentNullException(nameof(chapter.Files));
        }

        if (chapter.Files.Count == 0)
        {
            return string.Empty;
        }

        return format switch
        {
            DownloadFormat.Default => await CollectFiles(chapter, cancellationToken),
            DownloadFormat.Kepub => await ConvertToKepub(chapter, cancellationToken),
            DownloadFormat.Epub => await ConvertToEpub(chapter, cancellationToken),
            _ => throw new NotImplementedException($"Download format {format} not implemented")
        };
    }

    private async Task<string> ConvertToEpub(Chapter chapter, CancellationToken cancellationToken)
    {
        var format = chapter.Files.First().Format;
        if (format is MangaFormat.Epub)
        {
            return await CollectFiles(chapter, cancellationToken);
        }

        // TODO: Add support for transforming CBZ into epubs(kepub?) for KOBO
        throw new NotSupportedException();
    }

    private async Task<string> ConvertToKepub(Chapter chapter, CancellationToken cancellationToken)
    {
        var format = chapter.Files.First().Format;

        if (format is not MangaFormat.Epub || chapter.Files.Count != 1)
        {
            throw new NotSupportedException();
        }

        var outputPath = kepubifyConverterService.BuildKepubifyPath(chapter.Id);

        await kepubifyConverterService.ConvertEpubToKepub(chapter.Files.First().FilePath, outputPath, cancellationToken);

        return outputPath;
    }

    private async Task<string> CollectFiles(Chapter chapter, CancellationToken cancellationToken)
    {
        if (chapter.Files.Count <= 1)
        {
            return chapter.Files.First().FilePath;
        }

        var format = chapter.Files.First().Format;
        if (format is not MangaFormat.Archive)
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
