using System.Collections.Generic;
using System.Threading.Tasks;
using API.Data.Metadata;
using API.Parser;
using VersOne.Epub;

namespace API.Interfaces.Services
{
    public interface IBookService
    {
        int GetNumberOfPages(string filePath);
        string GetCoverImage(string fileFilePath, string fileName);
        Task<Dictionary<string, int>> CreateKeyToPageMappingAsync(EpubBookRef book);

        /// <summary>
        /// Scopes styles to .reading-section and replaces img src to the passed apiBase
        /// </summary>
        /// <param name="stylesheetHtml"></param>
        /// <param name="apiBase"></param>
        /// <param name="filename">If the stylesheetHtml contains Import statements, when scoping the filename, scope needs to be wrt filepath.</param>
        /// <param name="book">Book Reference, needed for if you expect Import statements</param>
        /// <returns></returns>
        Task<string> ScopeStyles(string stylesheetHtml, string apiBase, string filename, EpubBookRef book);
        string GetSummaryInfo(string filePath);
        ComicInfo GetComicInfo(string filePath);
        ParserInfo ParseInfo(string filePath);
        /// <summary>
        /// Extracts a PDF file's pages as images to an target directory
        /// </summary>
        /// <param name="fileFilePath"></param>
        /// <param name="targetDirectory">Where the files will be extracted to. If doesn't exist, will be created.</param>
        void ExtractPdfImages(string fileFilePath, string targetDirectory);
    }
}
