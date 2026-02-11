using System;
using System.Collections.Generic;
using Kavita.Models.Entities;

namespace Kavita.API.Services;

public interface IDownloadService
{
    Tuple<string, string, string> GetFirstFileDownload(IEnumerable<MangaFile> files);
    string GetContentTypeFromFile(string filepath);
}
