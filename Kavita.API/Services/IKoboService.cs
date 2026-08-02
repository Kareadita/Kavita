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
    /// Incremental library sync page (max 100). Persists synced-set rows; no ReadingState objects.
    /// </summary>
    Task<KoboLibrarySyncResult> SyncLibraryAsync(string authToken, string? syncTokenHeader,
        CancellationToken ct = default);

    /// <summary>
    /// Fresh metadata (including DownloadUrls Format EPUB) for one entitlement UUID.
    /// </summary>
    Task<IReadOnlyList<JsonObject>> GetMetadataAsync(string authToken, string entitlementId,
        CancellationToken ct = default);

    /// <summary>
    /// Serves the preferred native EPUB for the entitlement UUID.
    /// </summary>
    Task<KoboDownloadResult> GetDownloadAsync(string authToken, string entitlementId, string format,
        CancellationToken ct = default);

    /// <summary>
    /// Resolves cover bytes path for the entitlement UUID (chapter → volume → series).
    /// </summary>
    Task<KoboCoverResult?> GetCoverAsync(string authToken, string entitlementId,
        CancellationToken ct = default);

    /// <summary>Empty keep-alive stub body (<c>{}</c>).</summary>
    object GetEmptyStub();

    /// <summary>Calibre-Web loyalty benefits stub: <c>{"Benefits":{}}</c>.</summary>
    object GetLoyaltyBenefitsStub();

    /// <summary>Calibre-Web analytics gettests stub.</summary>
    object GetAnalyticsTestsStub(string? koboUserKey);

    /// <summary>Reading-state GET ACK stub (no persistence).</summary>
    object GetReadingStateStub(string entitlementId);

    /// <summary>Reading-state PUT ACK stub (no persistence).</summary>
    object PutReadingStateStub(string entitlementId);
}
