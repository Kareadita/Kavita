using System.Threading.Tasks;
using API.Parser;

namespace API.Entities.Interfaces
{
    public interface IBookService
    {
        int GetNumberOfPages(string filePath);

        ParserInfo ParseInfo(string filePath);
        byte[] GetCoverImage(string fileFilePath, bool createThumbnail = true);
        void ExtractToFolder(string fileFilePath, string destDirectory);
        /// <summary>
        /// Rewrites hrefs and srcs that contain ../ to an API endpoint, so we can appropriately serve them.
        /// </summary>
        /// <param name="archiveFile"></param>
        void MapHtmlFiles(string archiveFile);
    }
}