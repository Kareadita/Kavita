using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Kobo;

namespace Kavita.API.Services;

public interface IKoboService
{
    /// <summary>
    /// Returns the user's Kobo sync URL, lazily minting a named <c>kobo</c> AuthKey when missing.
    /// Requires EnableKoboSync and a non-empty HostName.
    /// </summary>
    Task<string> GetOrCreateSyncUrlAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Rotates the user's <c>kobo</c> AuthKey so previous sync URLs stop working.
    /// </summary>
    Task<string> RotateSyncAuthKeyAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Revokes the user's <c>kobo</c> AuthKey until Create/View mints a new one.
    /// </summary>
    Task RevokeSyncAuthKeyAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Resolves a Kobo URL token to the owning user id.
    /// Rejects missing, expired, revoked, or non-<c>kobo</c> keys, and when the feature is disabled.
    /// </summary>
    Task<int> ResolveUserIdAsync(string authToken, CancellationToken ct = default);

    /// <summary>
    /// Builds initialization Resources with image_host, cover templates, and library_sync
    /// rewritten from configured HostName + BaseUrl (not the device request host).
    /// </summary>
    Task<KoboInitializationResult> GetInitializationAsync(string authToken, CancellationToken ct = default);

    /// <summary>
    /// Returns dummy bearer token JSON for auth/device and auth/refresh.
    /// </summary>
    Task<KoboAuthTokenDto> CreateDeviceAuthResponseAsync(string authToken, string? userKey,
        CancellationToken ct = default);

    /// <summary>
    /// Incremental library sync page (admin-configurable size). Persists synced-set rows.
    /// Serializes concurrent calls per user; may throw <c>kobo-sync-busy</c> (503) after a 30s wait.
    /// </summary>
    Task<KoboLibrarySyncResult> SyncLibraryAsync(string authToken, string? syncTokenHeader,
        CancellationToken ct = default);

    /// <summary>
    /// Fresh metadata (including DownloadUrls Format EPUB) for one entitlement UUID.
    /// </summary>
    Task<IReadOnlyList<JsonObject>> GetMetadataAsync(string authToken, string entitlementId,
        CancellationToken ct = default);

    /// <summary>
    /// Serves the preferred native EPUB, or a CBZ/CBR converted EPUB from the shared cache.
    /// May throw <c>kobo-convert-unavailable</c> (503) or <c>kobo-convert-failed</c> (500).
    /// </summary>
    Task<KoboDownloadResult> GetDownloadAsync(string authToken, string entitlementId, string format,
        CancellationToken ct = default);

    /// <summary>
    /// Resolves cover bytes path for the entitlement UUID (chapter → volume → series).
    /// </summary>
    Task<KoboCoverResult?> GetCoverAsync(string authToken, string entitlementId,
        CancellationToken ct = default);

    /// <summary>
    /// Device DELETE: archive for the user, drop synced-set row, leave chapter in Kavita.
    /// </summary>
    Task DeleteEntitlementAsync(string authToken, string entitlementId, CancellationToken ct = default);

    /// <summary>
    /// Clears the user's synced-set rows only. Does not rotate the AuthKey or clear archives.
    /// </summary>
    Task ForceFullSyncAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Bulk-restores books the user removed on the device: clears device-deleted archives for
    /// still-eligible chapters and drops those chapters from the synced-set. Tombstones / hard
    /// deletes are not restored. Does not clear eligibility archives (those auto-unarchive).
    /// </summary>
    Task RestoreRemovedBooksAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Before hard-deleting chapters: for users who had them synced, create tombstones and
    /// archive-for-removal so the next sync can emit <c>IsRemoved</c>.
    /// </summary>
    Task PrepareHardDeleteAsync(IEnumerable<int> chapterIds, CancellationToken ct = default);

    /// <summary>Empty keep-alive stub body (<c>{}</c>).</summary>
    object GetEmptyStub();

    /// <summary>Calibre-Web loyalty benefits stub: <c>{"Benefits":{}}</c>.</summary>
    object GetLoyaltyBenefitsStub();

    /// <summary>Calibre-Web analytics gettests stub.</summary>
    object GetAnalyticsTestsStub(string? koboUserKey);

    /// <summary>
    /// GET reading-state shaped from <c>AppUserProgress</c> (ReadyToRead when none).
    /// </summary>
    Task<object> GetReadingStateAsync(string authToken, string entitlementId,
        CancellationToken ct = default);

    /// <summary>
    /// PUT reading-state into <c>AppUserProgress</c> with last-write-wins on timestamps.
    /// Ignores Statistics and Location. ACK shape matches Calibre-Web.
    /// </summary>
    Task<object> PutReadingStateAsync(string authToken, string entitlementId, JsonObject? body,
        CancellationToken ct = default);
}
