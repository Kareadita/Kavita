using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.KavitaPlus.OAuth;

namespace Kavita.API.Services.Plus;

public interface IOAuthService
{
    Task HandleCallback(OAuthUpstream upstream, string token, string? refreshToken = null);

    Task RefreshTokens(CancellationToken ct = default);
}
