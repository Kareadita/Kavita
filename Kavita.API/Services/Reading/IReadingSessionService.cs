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

    /// <summary>
    /// Records reading that a client did earlier and could not report at the time (i.e. it was offline), as finished
    /// reading sessions with the times the client measured. Each chapter's progress moves to where the latest
    /// reading of it ended, unless the server has seen progress on that chapter since.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="entries">Validated one by one; an entry that is refused does not stop the others</param>
    /// <param name="ct"></param>
    /// <returns>What became of each entry, in the order given</returns>
    Task<IList<BackfillReadingStatus>> BackfillReadingSessions(int userId, IList<BackfillReadingEntryDto> entries, CancellationToken ct = default);

}
