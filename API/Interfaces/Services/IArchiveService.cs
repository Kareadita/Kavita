using API.Archive;

namespace API.Interfaces.Services
{
    public interface IArchiveService
    {
        void ExtractArchive(string archivePath, string extractPath);
        int GetNumberOfPagesFromArchive(string archivePath);
        byte[] GetCoverImage(string filepath, bool createThumbnail = false);
        bool IsValidArchive(string archivePath);
        string GetSummaryInfo(string archivePath);
        ArchiveMetadata GetArchiveData(string archivePath, bool createThumbnail);
    }
}