using System.IO.Compression;
using API.Archive;

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
    }
}