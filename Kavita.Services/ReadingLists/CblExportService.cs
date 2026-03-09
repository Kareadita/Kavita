using System;
using System.Threading.Tasks;

namespace Kavita.Services.ReadingLists;

public class CblExportService
{
    /// <summary>
    /// Exports the reading list to a temp/{reading list name}-{userid}
    /// </summary>
    /// <param name="readingListId"></param>
    /// <param name="asV2"></param>
    /// <returns></returns>
    Task<string> ExportReadingList(int readingListId, int userId, bool asV2 = false)
    {
        throw new NotImplementedException();
    }
}
