using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Account;
using API.DTOs.Internal;
using API.Entities;
using API.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using static System.Security.Claims.ClaimTypes;
using JwtRegisteredClaimNames = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames;


namespace API.Services;
#nullable enable

public interface ITokenService
{
    Task<string> CreateToken(AppUser user);
    Task<TokenRequestDto?> ValidateRefreshToken(TokenRequestDto request);
    Task<string> CreateRefreshToken(AppUser user);
    Task<string?> GetJwtFromUser(AppUser user);
}


public class TokenService : ITokenService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<TokenService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly SymmetricSecurityKey _key;
    private const string RefreshTokenName = "RefreshToken";
    private static readonly SemaphoreSlim RefreshTokenLock = new(1, 1);

    public TokenService(IOptions<AppSettingsDto> config, UserManager<AppUser> userManager, ILogger<TokenService> logger, IUnitOfWork unitOfWork)
    {
        _userManager = userManager;
        _logger = logger;
        _unitOfWork = unitOfWork;
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.Value.TokenKey ?? string.Empty));
    }

    public async Task<string> CreateToken(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Name, user.UserName!),
            new(NameIdentifier, user.Id.ToString()),
        };

        var roles = await _userManager.GetRolesAsync(user);
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

    public async Task<string> CreateRefreshToken(AppUser user)
    {
        await _userManager.RemoveAuthenticationTokenAsync(user, TokenOptions.DefaultProvider, RefreshTokenName);
        var refreshToken = await _userManager.GenerateUserTokenAsync(user, TokenOptions.DefaultProvider, RefreshTokenName);
        await _userManager.SetAuthenticationTokenAsync(user, TokenOptions.DefaultProvider, RefreshTokenName, refreshToken);
        return refreshToken;
    }

    public async Task<TokenRequestDto?> ValidateRefreshToken(TokenRequestDto request)
    {
        await RefreshTokenLock.WaitAsync();

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenContent = tokenHandler.ReadJwtToken(request.Token);
            var username = tokenContent.Claims.FirstOrDefault(q => q.Type == JwtRegisteredClaimNames.Name)?.Value;
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogDebug("[RefreshToken] failed to validate due to not finding user in RefreshToken");
                return null;
            }

            var user = await _userManager.FindByNameAsync(username);
            if (user == null)
            {
                _logger.LogDebug("[RefreshToken] failed to validate due to not finding user in DB");
                return null;
            }

            var validated = await _userManager.VerifyUserTokenAsync(user, TokenOptions.DefaultProvider,
                RefreshTokenName, request.RefreshToken);
            if (!validated && tokenContent.ValidTo <= DateTime.UtcNow.Add(TimeSpan.FromHours(1)))
            {
                _logger.LogDebug("[RefreshToken] failed to validate due to invalid refresh token");
                return null;
            }

            // Remove the old refresh token first
            await _userManager.RemoveAuthenticationTokenAsync(user,
                TokenOptions.DefaultProvider,
                RefreshTokenName);

            try
            {
                await _unitOfWork.UserRepository.UpdateUserAsActive(user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update last active for {UserName}", user.UserName);
            }

            return new TokenRequestDto()
            {
                Token = await CreateToken(user),
                RefreshToken = await CreateRefreshToken(user)
            };
        }
        catch (SecurityTokenExpiredException ex)
        {
            // Handle expired token
            _logger.LogError(ex, "Failed to validate refresh token");
            return null;
        }
        catch (Exception ex)
        {
            // Handle other exceptions
            _logger.LogError(ex, "Failed to validate refresh token");
            return null;
        }
        finally
        {
            RefreshTokenLock.Release();
        }
    }

    public async Task<string?> GetJwtFromUser(AppUser user)
    {
        var userClaims = await _userManager.GetClaimsAsync(user);
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
