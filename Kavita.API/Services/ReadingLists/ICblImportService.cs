using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.ReadingLists.CBL;
using Kavita.Models.DTOs.ReadingLists.CBL.Import;

namespace Kavita.API.Services.ReadingLists;


public interface ICblImportService
{
    Task<CblImportSummaryDto> ValidateList(int userId, string filePath);
    /// <summary>
    /// Creates a new RL or updates an existing
    /// </summary>
    Task<CblImportSummaryDto> UpsertReadingList(int userId, string filePath, CblImportDecisions decisions);
    /// <summary>
    /// Checks for updates against upstream ReadingList files and attempts to Update reading list
    /// </summary>
    /// <remarks>Does not prompt for validation, makes best guess</remarks>
    Task SyncReadingListAsync(int userId, int readingListId, bool force = false);
    /// <summary>
    /// Iterates over all users and reading lists that are sync-applicable and attempts to sync them
    /// </summary>
    /// <returns></returns>
    Task SyncAllReadingLists(CancellationToken cancellationToken = default);
}
