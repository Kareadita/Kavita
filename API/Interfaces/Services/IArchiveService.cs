using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Threading.Tasks;
using API.Archive;
using API.Entities;

namespace API.Interfaces.Services
{
    public interface IArchiveService
    {
        void ExtractArchive(string archivePath, string extractPath);
        int GetNumberOfPagesFromArchive(string archivePath);
        byte[] GetCoverImage(string archivePath, bool createThumbnail = false);
        bool IsValidArchive(string archivePath);
        string GetSummaryInfo(string archivePath);
        ArchiveLibrary CanOpen(string archivePath);
        bool ArchiveNeedsFlattening(ZipArchive archive);
        Task<Tuple<byte[], string>> CreateZipForDownload(IEnumerable<string> files, string tempFolder);
    }
}