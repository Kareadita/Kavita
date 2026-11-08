using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Kobo;
using Kavita.Models.Entities.Kobo;
using Kavita.Models.Entities.User;

namespace Kavita.API.Services;

public interface IKoboSyncService
{
    /// <summary>
    /// Accumulate all changes since the syncPoint
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="syncPreferences"></param>
    /// <param name="syncPoint"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<(List<KoboSyncItem>, bool)> GetChangesSinceAsync(int userId, AppUserKoboSyncPreferences syncPreferences, KoboSyncPoint syncPoint, CancellationToken cancellationToken);

    /// <summary>
    /// Builds book metadata for the given ids. They are not checked against access; only against sync settings in this method. The caller is responsible
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="chapterIds"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Dictionary<int, KoboBookMetadataDto>> BuildBookMetadata(int userId, IEnumerable<int> chapterIds, CancellationToken cancellationToken);

    Task<IList<KoboAnnotationDto>> GetKoboAnnotationsAsync(int userId, IEnumerable<int> chapterIds, CancellationToken cancellationToken);
    Task UpdateKoboAnnotationsAsync(int userId, int chapterId, List<KoboAnnotationDto> annotationDtos, CancellationToken cancellationToken);

    Task<KoboReadingStateDto?> GetProgress(int userId, int chapterId, CancellationToken cancellationToken);
    Task<KoboStateUpdateResultDto> SaveProgress(int userId, int chapterId, KoboReadingStateDto readingStateDto, CancellationToken cancellationToken);

    Task SyncXPathToKoboSpan(int progressId, CancellationToken cancellationToken);
}
