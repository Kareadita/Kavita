using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.MediaErrors;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Parser;

namespace Kavita.API.Repositories;

public interface IMediaErrorRepository
{
    void Attach(MediaError error);
    void Remove(IList<MediaError> errors);
    Task<IEnumerable<MediaErrorDto>> GetAllErrorDtosAsync(CancellationToken ct = default);
    Task DeleteAll(CancellationToken ct = default);
    /// <summary>
    /// The row a producer other than the scanner wrote for this file and reason
    /// </summary>
    Task<MediaError?> GetProducerErrorAsync(string filePath, MediaErrorReason reason, CancellationToken ct = default);
    /// <summary>
    /// Every row in this library written by a producer other than the scanner
    /// </summary>
    Task<List<MediaError>> GetProducerErrorsAsync(int libraryId, CancellationToken ct = default);
    /// <summary>
    /// The file path of every row in this library, from any producer, by row id
    /// </summary>
    Task<Dictionary<int, string>> GetFilePathsAsync(int libraryId, CancellationToken ct = default);
    Task DeleteAsync(IList<int> ids, CancellationToken ct = default);
    /// <summary>
    /// The library and series of the file with this path. Falls back to the library whose folder holds the path when no series has the file yet
    /// </summary>
    Task<MediaErrorOwner> GetOwnerAsync(string filePath, CancellationToken ct = default);
    /// <summary>
    /// Files the scanner had an issue with in this library, with the stamp they had then. Dismissed rows are included
    /// </summary>
    Task<List<FailedFile>> GetFailedFilesAsync(int libraryId, CancellationToken ct = default);
    /// <summary>
    /// The scanner's rows for these paths
    /// </summary>
    Task<List<MediaError>> GetScannerErrorsAsync(int libraryId, IList<string> filePaths, CancellationToken ct = default);
    /// <summary>
    /// Gives each of these scanner rows with no series the series of its own file, else the only series with files in the same folder,
    /// else <paramref name="scannedSeriesId"/> when no series has files there. Does not commit
    /// </summary>
    /// <param name="filesByFolder">Every file listed directly in each row's folder</param>
    /// <param name="scannedSeriesId">The series a series scan was run for</param>
    Task AssignScannerErrorsToSeriesAsync(int libraryId, IList<string> filePaths,
        IReadOnlyDictionary<string, IList<string>> filesByFolder, int? scannedSeriesId = null, CancellationToken ct = default);
    /// <summary>
    /// Files in this library the scanner could not import, dismissed ones left out
    /// </summary>
    Task<int> GetUnreadableFileCountAsync(int libraryId, CancellationToken ct = default);
    /// <inheritdoc cref="GetUnreadableFileCountAsync"/>
    /// <remarks>Most recently seen first</remarks>
    Task<List<ScanIssueSummaryItemDto>> GetUnreadableFilesAsync(int libraryId, int take, CancellationToken ct = default);

    Task SetDismissStateAsync(List<int> errorIds, bool dismissState, CancellationToken ct = default);
    Task<List<MediaErrorDto>> GetErrorDtosForSeriesAsync(int seriesId, CancellationToken ct);
}
