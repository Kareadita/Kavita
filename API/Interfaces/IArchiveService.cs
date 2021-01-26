using System.IO.Compression;

namespace API.Interfaces
{
    public interface IArchiveService
    {
        bool ArchiveNeedsFlattening(ZipArchive archive);
        public void ExtractArchive(string archivePath, string extractPath);
    }
}