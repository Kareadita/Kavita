using System.Threading.Tasks;
using Kavita.Models.DTOs.Account;
using Kavita.Models.Entities.User;

namespace Kavita.API.Services;

public interface ITokenService
{
    Task<string> CreateToken(AppUser user);
    Task<TokenRequestDto?> ValidateRefreshToken(TokenRequestDto request);
    Task<string> CreateRefreshToken(AppUser user);
    Task<string?> GetJwtFromUser(AppUser user);
}
