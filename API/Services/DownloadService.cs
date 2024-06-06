using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using API.Entities;
using Microsoft.AspNetCore.StaticFiles;
using MimeTypes;

namespace API.Services;

public interface IDownloadService
{
    Tuple<string, string, string> GetFirstFileDownload(IEnumerable<MangaFile> files);
    string GetContentTypeFromFile(string filepath);
}
public class DownloadService : IDownloadService
{
    private readonly FileExtensionContentTypeProvider _fileTypeProvider = new FileExtensionContentTypeProvider();

    public DownloadService() { }

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


}
