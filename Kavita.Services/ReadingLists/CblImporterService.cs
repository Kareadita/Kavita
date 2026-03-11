using System.Threading.Tasks;
using Kavita.API.Services.ReadingLists;
using Kavita.Models.DTOs.ReadingLists.CBL;

namespace Kavita.Services.ReadingLists;

public class CblImporterService : ICblImportService
{
    public Task ValidateList(int userId, string filePath, CblImportOptions options)
    {

        throw new System.NotImplementedException();
    }

    public Task UpsertReadingList(int userId, string filePath, CblImportOptions options, CblImportDecisions decisions)
    {
        throw new System.NotImplementedException();
    }

    public Task SyncReadingList(int userId, int readingListId)
    {
        throw new System.NotImplementedException();
    }
}
