using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using API.DTOs.Account;
using API.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using static System.Security.Claims.ClaimTypes;
using JwtRegisteredClaimNames = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames;


namespace API.Services;

public interface ITokenService
{
    Task<string> CreateToken(AppUser user);
    Task<TokenRequestDto?> ValidateRefreshToken(TokenRequestDto request);
    Task<string> CreateRefreshToken(AppUser user);
}


public class TokenService : ITokenService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SymmetricSecurityKey _key;
    private const string RefreshTokenName = "RefreshToken";

    public TokenService(IConfiguration config, UserManager<AppUser> userManager)
    {

        _userManager = userManager;
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["TokenKey"] ?? string.Empty));
    }

    public async Task<string> CreateToken(AppUser user)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Name, user.UserName!),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        };

        var roles = await _userManager.GetRolesAsync(user);

        claims.AddRange(roles.Select(role => new Claim(Role, role)));

        var credentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha512Signature);
        var tokenDescriptor = new SecurityTokenDescriptor()
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(14),
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
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenContent = tokenHandler.ReadJwtToken(request.Token);
            var username = tokenContent.Claims.FirstOrDefault(q => q.Type == JwtRegisteredClaimNames.Name)?.Value;
            if (string.IsNullOrEmpty(username)) return null;
            var user = await _userManager.FindByNameAsync(username);
            if (user == null) return null; // This forces a logout
            var validated = await _userManager.VerifyUserTokenAsync(user, TokenOptions.DefaultProvider, RefreshTokenName, request.RefreshToken);
            if (!validated) return null;
            await _userManager.UpdateSecurityStampAsync(user);

            return new TokenRequestDto()
            {
                Token = await CreateToken(user),
                RefreshToken = await CreateRefreshToken(user)
            };
        } catch (SecurityTokenExpiredException)
        {
            // Handle expired token
            return null;
        }
        catch (Exception)
        {
            // Handle other exceptions
            return null;
        }
    }
}
