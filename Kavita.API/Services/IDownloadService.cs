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
    /// <summary>
    /// Prepare a chapter for download, and return the path to the file to download. This may be the unmodified library file
    /// if possible.
    /// </summary>
    Task<string> DownloadChapter(int userId, Chapter chapter, CancellationToken cancellationToken);
}
