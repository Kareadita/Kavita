using API.Parser;

namespace API.Entities.Interfaces
{
    public interface IBookService
    {
        int GetNumberOfPages(string filePath);

        ParserInfo ParseInfo(string filePath);
        byte[] GetCoverImage(string fileFilePath, bool createThumbnail = true);
        void ExtractToFolder(string fileFilePath, string destDirectory);
    }
}