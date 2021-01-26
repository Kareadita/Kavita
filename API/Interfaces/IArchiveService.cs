using System.IO.Compression;

namespace API.Interfaces
{
    public interface IArchiveService
    {
        bool ArchiveNeedsFlattening(ZipArchive archive);
        void ExtractArchive(string archivePath, string extractPath);
        int GetNumberOfPagesFromArchive(string archivePath);
        byte[] GetCoverImage(string filepath, bool createThumbnail = false);
    }
}