using System;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using API.Extensions;
using API.Services;
using Kavita.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace API.Helpers;
#nullable enable

public class OpenIdConnectEventsHelper: OpenIdConnectEvents
{
    private const string ApiPrefix = "/api";
    private const string HubsPrefix = "/hubs";

    private readonly string _baseUrl;
    private readonly bool _isDevelopment;

    public OpenIdConnectEventsHelper(string baseUrl, bool isDevelopment)
    {
        _baseUrl = baseUrl;
        _isDevelopment = isDevelopment;

        OnTicketReceived = HandleTicketReceived;
        OnUserInformationReceived = HandleUserInformationReceived;
        OnAuthenticationFailed = HandleAuthenticationFailure;
        OnRedirectToIdentityProviderForSignOut = HandleRedirectToIdentityProviderForSignOut;
        OnRedirectToIdentityProvider = HandleRedirectToIdentityProvider;
        OnRemoteFailure = HandleRemoteFailure;
    }

    private Task HandleRemoteFailure(RemoteFailureContext ctx)
    {
        if (ctx.Failure == null)
            return Task.CompletedTask;

        Log.Error(ctx.Failure, "Encountered an exception while communicating with the idp");
        ctx.Response.Redirect(_baseUrl + "login?skipAutoLogin=true&error=" + Uri.EscapeDataString(ctx.Failure.Message));
        ctx.HandleResponse();

        return Task.CompletedTask;
    }

    private Task HandleRedirectToIdentityProvider(RedirectContext ctx)
    {
        // Intercept redirects on API requests and instead return 401
        // These redirects are auto login when .NET finds a cookie that it can't match inside the cookie store. I.e. after a restart
        if (ctx.Request.Path.StartsWithSegments(ApiPrefix) || ctx.Request.Path.StartsWithSegments(HubsPrefix))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            ctx.HandleResponse();
            return Task.CompletedTask;
        }

        if (!_isDevelopment && !string.IsNullOrEmpty(ctx.ProtocolMessage.RedirectUri))
        {
            ctx.ProtocolMessage.RedirectUri = ctx.ProtocolMessage.RedirectUri.Replace("http://", "https://");
        }

        return Task.CompletedTask;
    }

    private Task HandleRedirectToIdentityProviderForSignOut(RedirectContext ctx)
    {
        if (!_isDevelopment && !string.IsNullOrEmpty(ctx.ProtocolMessage.PostLogoutRedirectUri))
        {
            ctx.ProtocolMessage.PostLogoutRedirectUri = ctx.ProtocolMessage.PostLogoutRedirectUri.Replace("http://", "https://");
        }

        return Task.CompletedTask;
    }

    private Task HandleAuthenticationFailure(AuthenticationFailedContext ctx)
    {
        ctx.Response.Redirect(_baseUrl + "login?skipAutoLogin=true&error=" + Uri.EscapeDataString(ctx.Exception.Message));
        ctx.HandleResponse();

        return Task.CompletedTask;
    }

    private static Task HandleUserInformationReceived(UserInformationReceivedContext ctx)
    {
        if (ctx.Principal?.Identity == null)
        {
            return Task.CompletedTask;
        }

        var identity = (ClaimsIdentity) ctx.Principal.Identity;

        // Copy all claims over as in, the ones we need mapped to something specific are above
        foreach (var property in ctx.User.RootElement.EnumerateObject())
        {
            var claimType = property.Name;
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in property.Value.EnumerateArray())
                {
                    identity.AddClaim(new Claim(claimType, element.ToString(), ClaimValueTypes.String, IdentityServiceExtensions.OpenIdConnect));
                }
            }
            else
            {
                identity.AddClaim(new Claim(claimType, property.Value.ToString(), ClaimValueTypes.String, IdentityServiceExtensions.OpenIdConnect));
            }
        }
        return Task.CompletedTask;
    }

    private async Task HandleTicketReceived(TicketReceivedContext ctx)
    {
        try
        {
            await OidcClaimsPrincipalConverter(ctx);
        }
        catch (KavitaException ex)
        {
            Log.Error(ex, "An exception occured during initial OIDC flow");
            ctx.Response.Redirect(_baseUrl + "login?skipAutoLogin=true&error=" + Uri.EscapeDataString(ex.Message));
            ctx.HandleResponse();
        }
    }

    /// <summary>
    /// Called after the redirect from the OIDC provider, tries matching the user and update the principal
    /// to have the correct claims and properties. This is required to later auto refresh; and ensure .NET knows which
    /// Kavita roles the user has
    /// </summary>
    /// <param name="ctx"></param>
    private static async Task OidcClaimsPrincipalConverter(TicketReceivedContext ctx)
    {
        if (ctx.Principal == null) return;

        var oidcService = ctx.HttpContext.RequestServices.GetRequiredService<IOidcService>();
        var user = await oidcService.LoginOrCreate(ctx.Request, ctx.Principal);
        if (user == null)
        {
            throw new KavitaException("errors.oidc.no-account");
        }

        var claims = await OidcService.ConstructNewClaimsList(ctx.HttpContext.RequestServices, ctx.Principal, user);

        var identity = new ClaimsIdentity(claims, ctx.Scheme.Name);
        var principal = new ClaimsPrincipal(identity);

        ctx.HttpContext.User = principal;
        ctx.Principal = principal;

        ctx.Success();
    }

}
