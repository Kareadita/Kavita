using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.Models.Constants;
using Kavita.Models.Entities.Progress;
using Kavita.Server.Attributes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kavita.Server.Middleware;

public class KoboDeviceAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "KoboDevice";
}

public class KoboDeviceAuthenticationHandler(
    IOptionsMonitor<KoboDeviceAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IUnitOfWork unitOfWork)
    : AuthenticationHandler<KoboDeviceAuthenticationOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Context.GetEndpoint()?.Metadata.GetMetadata<KoboDeviceAuthenticateAttribute>() is null)
        {
            return AuthenticateResult.NoResult();
        }

        var deviceId = Request.Headers[KoboHeaders.KoboDeviceId].FirstOrDefault();
        if (string.IsNullOrEmpty(deviceId))
        {
            return AuthenticateResult.NoResult();
        }

        try
        {
            var userIds = await unitOfWork.DataContext.KoboSyncPoints
                .Where(sp => sp.KoboDeviceId == deviceId)
                .Select(sp => sp.UserId)
                .Distinct()
                .ToListAsync(Context.RequestAborted);

            if (userIds.Count != 1)
            {
                return AuthenticateResult.Fail(userIds.Count == 0
                    ? "Unknown Kobo device"
                    : "Kobo device is linked to multiple users");
            }

            var user = await unitOfWork.UserRepository.GetUserByIdAsync(userIds[0], ct: Context.RequestAborted);
            if (user is null || string.IsNullOrEmpty(user.UserName))
            {
                return AuthenticateResult.Fail("Kobo device user not found");
            }

            var roles = await unitOfWork.UserRepository.GetRoles(user.Id, Context.RequestAborted);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Name, user.UserName),
                new("AuthType", nameof(AuthenticationType.KoboDevice)),
            };
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
        catch (OperationCanceledException)
        {
            return AuthenticateResult.Fail("Kobo device authentication failed");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Kobo device authentication failed");
            return AuthenticateResult.Fail("Kobo device authentication failed");
        }
    }
}
