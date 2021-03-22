using System.IO.Compression;
using API.Entities;

namespace API.Interfaces.Services
{
    public interface IArchiveService
    {
        void ExtractArchive(string archivePath, string extractPath);
        int GetNumberOfPagesFromArchive(string archivePath);
        byte[] GetCoverImage(string filepath, bool createThumbnail = false);
        bool IsValidArchive(string archivePath);
        string GetSummaryInfo(string archivePath);
    }
}