using System.Threading.Tasks;
using Kavita.Models.DTOs.ReadingLists.CBL;

namespace Kavita.API.Services.ReadingLists;


public interface ICblImportService
{
    Task ValidateList(int userId, string filePath, CblImportOptions options);
    /// <summary>
    /// Creates a new RL or updates an existing
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="filePath"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    Task UpsertReadingList(int userId, string filePath, CblImportOptions options, CblImportDecisions decisions);
    /// <summary>
    /// Checks for updates against upstream ReadingList files and attempts to Update reading list.
    /// </summary>
    /// <remarks>Does not prompt for validation, makes best guess</remarks>
    /// <param name="userId"></param>
    /// <param name="readingListId"></param>
    /// <returns></returns>
    Task SyncReadingList(int userId, int readingListId);
}
