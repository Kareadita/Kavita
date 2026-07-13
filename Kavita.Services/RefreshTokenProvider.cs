using System;
using Kavita.Models.Entities.User;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kavita.Services;

/// <summary>
/// Options for the dedicated refresh token provider. Uses a long lifespan so that infrequently-used devices
/// (e.g. a tablet that is asleep for days) can still refresh their session. Kept separate from the Default
/// provider so this lifespan does not apply to password-reset or email-confirmation tokens.
/// </summary>
public class RefreshTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public RefreshTokenProviderOptions()
    {
        Name = TokenService.RefreshTokenProviderName;
        TokenLifespan = TimeSpan.FromDays(30);
    }
}

/// <summary>
/// A <see cref="DataProtectorTokenProvider{TUser}"/> configured via <see cref="RefreshTokenProviderOptions"/> so
/// refresh tokens have their own lifespan independent of the Default provider.
/// </summary>
public class RefreshTokenProvider(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<RefreshTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<AppUser>> logger)
    : DataProtectorTokenProvider<AppUser>(dataProtectionProvider, options, logger);
