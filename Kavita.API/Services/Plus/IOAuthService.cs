using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.KavitaPlus.OAuth;
using Kavita.Models.Entities.User;

namespace Kavita.API.Services.Plus;

public interface IOAuthService
{
    Task HandleCallback(AppUser user, OAuthUpstream upstream, string token, string? refreshToken = null);

    Task RefreshTokens(CancellationToken ct = default);
}
