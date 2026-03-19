using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Progress;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Progress;

namespace Kavita.API.Services.Reading;

public interface IReadingSessionService
{
    Task UpdateProgress(int userId, ProgressDto progressDto, ClientInfoData? clientInfo, int? deviceId);

    /// <summary>
    /// Generate new reading sessions for all chapters in the given series
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task GenerateReadingSessionForSeries(int userId, int seriesId, CancellationToken ct = default);
    /// <summary>
    /// Generate new reading sessions for all volumes in the given series (Incorrect volumeIds are ignored)
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    /// <param name="volumeIds"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task GenerateReadingSessionForVolumes(int userId, int seriesId, List<int> volumeIds, CancellationToken ct = default);
    /// <summary>
    /// Generate new reading sessions for all chapters in the given series (Incorrect chapterIds are ignored)
    ///
    /// Chapters will be read in ascending <see cref="Chapter.SortOrder"/>.
    /// With the last chapter being finished reading now
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    /// <param name="chapterIds"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task GenerateReadingSessionForChapters(int userId, int seriesId, List<int> chapterIds, CancellationToken ct = default);

}
