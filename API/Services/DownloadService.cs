using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using Microsoft.AspNetCore.StaticFiles;

namespace API.Services;

public interface IDownloadService
{
    Task<(byte[], string, string)> GetFirstFileDownload(IEnumerable<MangaFile> files);
    string GetContentTypeFromFile(string filepath);
}
public class DownloadService : IDownloadService
{
    private readonly IDirectoryService _directoryService;
    private readonly FileExtensionContentTypeProvider _fileTypeProvider = new FileExtensionContentTypeProvider();

    public DownloadService(IDirectoryService directoryService)
    {
        _directoryService = directoryService;
    }

    /// <summary>
    /// Downloads the first file in the file enumerable for download
    /// </summary>
    /// <param name="files"></param>
    /// <returns></returns>
    public async Task<(byte[], string, string)> GetFirstFileDownload(IEnumerable<MangaFile> files)
    {
        var firstFile = files.Select(c => c.FilePath).First();
        return (await _directoryService.ReadFileAsync(firstFile), GetContentTypeFromFile(firstFile), Path.GetFileName(firstFile));
    }

    public string GetContentTypeFromFile(string filepath)
    {
        // Figures out what the content type should be based on the file name.
        if (!_fileTypeProvider.TryGetContentType(filepath, out var contentType))
        {
            contentType = Path.GetExtension(filepath).ToLowerInvariant() switch
            {
                ".cbz" => "application/zip",
                ".cbr" => "application/vnd.rar",
                ".cb7" => "application/x-compressed",
                ".epub" => "application/epub+zip",
                ".7z" => "application/x-7z-compressed",
                ".7zip" => "application/x-7z-compressed",
                ".pdf" => "application/pdf",
                _ => contentType
            };
        }

        return contentType;
    }
}
