using System.Threading.Tasks;

namespace Kavita.API.Services.ReadingLists;

public interface ICblExportService
{
    /// <summary>
    /// Exports the reading list to a temp file on disk.
    /// </summary>
    /// <remarks>Will overwrite existing files</remarks>
    /// <param name="readingListId"></param>
    /// <param name="userId"></param>
    /// <param name="asV2">Export as CBLv2 (JSON)</param>
    /// <returns>Full file path of the exported file, or null if reading list not found</returns>
    Task<string?> ExportReadingList(int readingListId, int userId, bool asV2 = false);
}
