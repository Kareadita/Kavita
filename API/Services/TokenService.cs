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
using JwtRegisteredClaimNames = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames;


namespace API.Services;

public interface ITokenService
{
    Task<string> CreateToken(AppUser user);
    Task<TokenRequestDto> ValidateRefreshToken(TokenRequestDto request);
    Task<string> CreateRefreshToken(AppUser user);
}

public class TokenService : ITokenService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SymmetricSecurityKey _key;

    public TokenService(IConfiguration config, UserManager<AppUser> userManager)
    {

        _userManager = userManager;
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["TokenKey"]));
    }

    public async Task<string> CreateToken(AppUser user)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.NameId, user.UserName)
        };

        var roles = await _userManager.GetRolesAsync(user);

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha512Signature);

        var tokenDescriptor = new SecurityTokenDescriptor()
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.Now.AddDays(14),
            SigningCredentials = creds
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }

    public async Task<string> CreateRefreshToken(AppUser user)
    {
        await _userManager.RemoveAuthenticationTokenAsync(user, TokenOptions.DefaultProvider, "RefreshToken");
        var refreshToken = await _userManager.GenerateUserTokenAsync(user, TokenOptions.DefaultProvider, "RefreshToken");
        await _userManager.SetAuthenticationTokenAsync(user, TokenOptions.DefaultProvider, "RefreshToken", refreshToken);
        return refreshToken;
    }

    public async Task<TokenRequestDto> ValidateRefreshToken(TokenRequestDto request)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenContent = tokenHandler.ReadJwtToken(request.Token);
        var username = tokenContent.Claims.FirstOrDefault(q => q.Type == JwtRegisteredClaimNames.NameId)?.Value;
        var user = await _userManager.FindByNameAsync(username);
        if (user == null) return null; // This forces a logout
        await _userManager.VerifyUserTokenAsync(user, TokenOptions.DefaultProvider, "RefreshToken", request.RefreshToken);

        await _userManager.UpdateSecurityStampAsync(user);

        return new TokenRequestDto()
        {
            Token = await CreateToken(user),
            RefreshToken = await CreateRefreshToken(user)
        };
    }
}
