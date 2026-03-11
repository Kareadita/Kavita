using System;
using System.Threading.Tasks;
using Kavita.Database;

namespace Kavita.Services.ReadingLists;

public class CblExportService(DataContext dataContext)
{
    /// <summary>
    /// Exports the reading list to a temp/{reading list name}-{userid}
    /// </summary>
    /// <param name="readingListId"></param>
    /// <param name="asV2">Export as CBLv2</param>
    /// <returns>Outputs a file in config/temp/userId/cbl-export/{rId}-name.{ext}</returns>
    Task<string> ExportReadingList(int readingListId, int userId, bool asV2 = false)
    {
        throw new NotImplementedException();
    }
}
