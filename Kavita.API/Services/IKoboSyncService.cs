using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Kobo;
using Kavita.Models.Entities.Kobo;
using Kavita.Models.Entities.User;
using Microsoft.AspNetCore.Mvc;

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
    /// <param name="chapterIds"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Dictionary<int, KoboBookMetadataDto>> BuildBookMetadata(IEnumerable<int> chapterIds,
        CancellationToken cancellationToken);

    Task<IList<KoboAnnotationDto>> GetKoboAnnotationsAsync(int userId, IEnumerable<int> chapterIds, CancellationToken cancellationToken);
    /// <summary>
    /// ETags for the user's annotations per chapter, used by Kobo's annotation sync
    /// </summary>
    Task<IReadOnlyDictionary<int, string>> GetKoboAnnotationETagsAsync(int userId, IEnumerable<int> chapterIds, CancellationToken cancellationToken);
    Task UpdateKoboAnnotationsAsync(int userId, int chapterId, KoboPatchAnnotationsDto patch, CancellationToken cancellationToken);

    Task<KoboReadingStateDto?> GetProgress(int userId, int chapterId, CancellationToken cancellationToken);
    Task<KoboStateUpdateResultDto> SaveProgress(int userId, int chapterId, KoboReadingStateDto readingStateDto, CancellationToken cancellationToken);

    Task SyncXPathToKoboSpan(int progressId, CancellationToken cancellationToken);

    /// <summary>
    /// Proxies the current request to the Kobo store if the user has forwarding enabled
    /// </summary>
    /// <param name="httpClient">Defaults to the Kobo store client</param>
    /// <param name="body"></param>
    Task<ActionResult> ProxyRequestIfEnabled(HttpClient? httpClient = null, object? body = null);

    Task<(JsonNode?, bool)> ProxyRequest(KoboSyncPoint? syncPoint, CancellationToken ct, bool copyResponse = true, object? body = null, HttpClient? httpClient = null, bool writeResponseOnError = true);
}
