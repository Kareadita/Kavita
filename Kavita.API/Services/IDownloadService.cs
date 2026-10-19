using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;

namespace Kavita.API.Services;

public interface IDownloadService
{
    Tuple<string, string, string> GetFirstFileDownload(IEnumerable<MangaFile> files);
    string GetContentTypeFromFile(string filepath);

    /// <summary>
    /// Prepare a chapter for download, and return the path to the file to download. This may be the unmodified library file
    /// if possible.
    /// </summary>
    /// <param name="chapter"></param>
    /// <param name="format"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> DownloadChapter(Chapter chapter, DownloadFormat format, CancellationToken cancellationToken);
}
