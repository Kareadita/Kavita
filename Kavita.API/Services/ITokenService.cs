using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Account;
using Kavita.Models.Entities.User;

namespace Kavita.API.Services;

public interface ITokenService
{
    Task<string> CreateToken(AppUser user, CancellationToken ct = default);
    Task<TokenRequestDto?> ValidateRefreshToken(TokenRequestDto request, CancellationToken ct = default);
    Task<string> CreateRefreshToken(AppUser user, CancellationToken ct = default);
    Task<string?> GetJwtFromUser(AppUser user, CancellationToken ct = default);
}
