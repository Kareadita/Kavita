using System.Threading;
using System.Threading.Tasks;

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
}
