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
    /// Generate new reading sessions for all chapters in the given series (Incorrect chapterIds are ignored)
    ///
    /// Chapters will be read in ascending <see cref="Chapter.SortOrder"/>.
    /// With the last chapter being finished reading now
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    /// <param name="chaptersMap">A dictionary mapping chapter ids to progress counts from where the session should be generated</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task GenerateReadingSessionForChapters(int userId, int seriesId, Dictionary<int, int> chaptersMap, CancellationToken ct = default);

}
