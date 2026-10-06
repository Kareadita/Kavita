using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.MediaErrors;
using Kavita.Models.Entities;
using Kavita.Models.Parser;

namespace Kavita.API.Repositories;

public interface IMediaErrorRepository
{
    void Attach(MediaError error);
    void Remove(IList<MediaError> errors);
    Task<IEnumerable<MediaErrorDto>> GetAllErrorDtosAsync(CancellationToken ct = default);
    Task<bool> ExistsAsync(MediaError error, CancellationToken ct = default);
    Task DeleteAll(CancellationToken ct = default);
    Task<List<MediaError>> GetAllErrorsAsync(IList<string> comments, CancellationToken ct = default);
    /// <summary>
    /// Files the scanner had an issue with in this library, with the stamp they had then. Dismissed rows are included
    /// </summary>
    Task<List<FailedFile>> GetFailedFilesAsync(int libraryId, CancellationToken ct = default);
    /// <summary>
    /// The scanner's rows for these paths, tracked
    /// </summary>
    Task<List<MediaError>> GetScannerErrorsAsync(int libraryId, IList<string> filePaths, CancellationToken ct = default);
    /// <summary>
    /// Gives each of these scanner rows with no series the only series with files in the same folder. Does not commit
    /// </summary>
    Task AssignScannerErrorsToSeriesAsync(int libraryId, IList<string> filePaths, CancellationToken ct = default);
    /// <summary>
    /// Files in this library the scanner could not import, dismissed ones left out
    /// </summary>
    Task<int> GetUnreadableFileCountAsync(int libraryId, CancellationToken ct = default);
    /// <inheritdoc cref="GetUnreadableFileCountAsync"/>
    /// <remarks>Most recently seen first</remarks>
    Task<List<ScanIssueSummaryItemDto>> GetUnreadableFilesAsync(int libraryId, int take, CancellationToken ct = default);
}
