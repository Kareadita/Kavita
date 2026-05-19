using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs.Account;
using Kavita.Models.DTOs.Internal;
using Kavita.Models.Entities.User;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using static System.Security.Claims.ClaimTypes;
using JwtRegisteredClaimNames = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames;


namespace Kavita.Services;


public class TokenService(
    IOptions<AppSettingsDto> config,
    UserManager<AppUser> userManager,
    ILogger<TokenService> logger)
    : ITokenService
{
    private static readonly SemaphoreSlim RefreshTokenLock = new(1, 1);

    private const string RefreshTokenName = "RefreshToken";
    private readonly SymmetricSecurityKey _key = new(Encoding.UTF8.GetBytes(config.Value.TokenKey));

    public async Task<string> CreateToken(AppUser user, CancellationToken ct = default)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Name, user.UserName!),
            new(NameIdentifier, user.Id.ToString()),
        };

        var roles = await userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(role => new Claim(Role, role)));

        var credentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha512Signature);
        var tokenDescriptor = new SecurityTokenDescriptor()
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(10),
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }

    public async Task<string> CreateRefreshToken(AppUser user, CancellationToken ct = default)
    {
        await userManager.RemoveAuthenticationTokenAsync(user, TokenOptions.DefaultProvider, RefreshTokenName);
        var refreshToken = await userManager.GenerateUserTokenAsync(user, TokenOptions.DefaultProvider, RefreshTokenName);
        await userManager.SetAuthenticationTokenAsync(user, TokenOptions.DefaultProvider, RefreshTokenName, refreshToken);
        return refreshToken;
    }

    public async Task<TokenRequestDto?> ValidateRefreshToken(TokenRequestDto request, CancellationToken ct = default)
    {
        await RefreshTokenLock.WaitAsync(ct);

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenValidationParams = new TokenValidationParameters()
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _key,
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidIssuer = "Kavita",
                NameClaimType = JwtRegisteredClaimNames.Name,
                RoleClaimType = "role",
            };

            var principal = tokenHandler.ValidateToken(request.Token, tokenValidationParams, out var tokenContent);
            var username = principal.Claims.FirstOrDefault(q => q.Type == JwtRegisteredClaimNames.Name)?.Value;

            if (string.IsNullOrEmpty(username))
            {
                logger.LogDebug("[RefreshToken] failed to validate due to not finding user in RefreshToken");
                return null;
            }

            var user = await userManager.FindByNameAsync(username);
            if (user == null)
            {
                logger.LogDebug("[RefreshToken] failed to validate due to not finding user in DB");
                return null;
            }

            var validated = await userManager.VerifyUserTokenAsync(user, TokenOptions.DefaultProvider,
                RefreshTokenName, request.RefreshToken);
            if (!validated)
            {
                logger.LogDebug("[RefreshToken] failed to validate due to invalid refresh token");
                return null;
            }

            if (tokenContent.ValidTo <= DateTime.UtcNow.Add(TimeSpan.FromHours(1)))
            {
                return null;
            }

            // Remove the old refresh token first
            await userManager.RemoveAuthenticationTokenAsync(user,
                TokenOptions.DefaultProvider,
                RefreshTokenName);

            return new TokenRequestDto()
            {
                Token = await CreateToken(user, ct),
                RefreshToken = await CreateRefreshToken(user, ct)
            };
        }
        catch (SecurityTokenExpiredException ex)
        {
            // Handle expired token
            logger.LogError(ex, "Failed to validate refresh token");
            return null;
        }
        catch (Exception ex)
        {
            // Handle other exceptions
            logger.LogError(ex, "Failed to validate refresh token");
            return null;
        }
        finally
        {
            RefreshTokenLock.Release();
        }
    }

    public async Task<string?> GetJwtFromUser(AppUser user, CancellationToken ct = default)
    {
        var userClaims = await userManager.GetClaimsAsync(user);
        var jwtClaim = userClaims.FirstOrDefault(claim => claim.Type == "jwt");
        return jwtClaim?.Value;
    }

    public static bool HasTokenExpired(string? token)
    {
        return !JwtHelper.IsTokenValid(token);
    }


    public static DateTime GetTokenExpiry(string? token)
    {
        return JwtHelper.GetTokenExpiry(token);
    }
}
