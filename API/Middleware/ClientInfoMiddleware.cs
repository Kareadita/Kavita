using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using API.Constants;
using API.Entities.Enums;
using API.Entities.Progress;
using API.Extensions;
using API.Helpers;
using API.Services.Reading;
using API.Services.Store;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace API.Middleware;


/// <summary>
/// Middleware that extracts client information from the HTTP request and makes it
/// available through IClientInfoAccessor for the duration of the request.
/// </summary>
public partial class ClientInfoMiddleware(RequestDelegate next, ILogger<ClientInfoMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IUserContext userContext)
    {
        var clientInfo = ExtractClientInfo(context, userContext);
        var clientFingerprint = context.Request.Headers[Headers.ClientDeviceFingerprint].ToString();

        ClientInfoAccessor.SetClientInfo(clientInfo);
        ClientInfoAccessor.SetUiFingerprint(clientFingerprint);

        await next(context);
    }

    private ClientInfoData ExtractClientInfo(HttpContext context, IUserContext userContext)
    {
        var userAgent = context.Request.Headers.UserAgent.ToString();
        var kavitaClient = context.Request.Headers[Headers.KavitaClient].ToString();
        var ipAddress = GetClientIpAddress(context);
        var authType = userContext.GetAuthenticationType();
        var platform = BrowserHelper.DetectPlatform(userAgent);

        // If custom Kavita header exists, parse it for rich info
        if (!string.IsNullOrEmpty(kavitaClient))
        {
            var parsed = ParseKavitaClientHeader(kavitaClient, userAgent);
            parsed.IpAddress = ipAddress;
            parsed.AuthType = authType;
            parsed.CapturedAt = DateTime.UtcNow;
            if (parsed.Platform == ClientDevicePlatform.Unknown)
            {
                parsed.Platform = platform;
            }

            return parsed;
        }

        // Fallback to basic UA parsing
        var clientType = BrowserHelper.DetermineClientType(userAgent, context.Request.Path.Value);
        return new ClientInfoData
        {
            UserAgent = userAgent,
            IpAddress = ipAddress,
            AuthType = authType,
            ClientType = clientType,
            Platform = platform,
            DeviceType = BrowserHelper.CoaxDeviceType(clientType, platform),
            CapturedAt = DateTime.UtcNow
        };
    }

    private ClientInfoData ParseKavitaClientHeader(string header, string fallbackUa)
    {
        try
        {
            // Parse: "web-app/1.2.3 (Chrome/120.0; Windows; Desktop; 1920x1080; landscape)"
            var match = UserAgentRegex().Match(header);

            if (match.Success)
            {
                // We can ignore if it fails or not as the default will be Unknown, which is fine
                EnumExtensions.TryParse<ClientDevicePlatform>(match.Groups["platform"].Value, out var clientDevicePlatform);

                return new ClientInfoData
                {
                    ClientType = ClientDeviceType.WebApp,
                    AppVersion = match.Groups["appVersion"].Value,
                    Browser = match.Groups["browser"].Value,
                    BrowserVersion = match.Groups["browserVersion"].Value,
                    Platform = clientDevicePlatform,
                    DeviceType = match.Groups["deviceType"].Value,
                    ScreenWidth = int.Parse(match.Groups["screenWidth"].Value),
                    ScreenHeight = int.Parse(match.Groups["screenHeight"].Value),
                    Orientation = match.Groups["orientation"].Success
                        ? match.Groups["orientation"].Value
                        : null,
                    UserAgent = fallbackUa
                };
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse X-Kavita-Client header: {Header}", header.Sanitize());
        }

        // Fallback if parsing fails
        return new ClientInfoData
        {
            UserAgent = fallbackUa,
            ClientType = ClientDeviceType.WebApp
        };
    }

    // TODO: Turn this into an extension?
    private static string GetClientIpAddress(HttpContext context)
    {
        // Check for X-Forwarded-For header (proxy/load balancer)
        var forwardedFor = context.Request.Headers[Headers.ForwardedFor].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // Take the first IP in the chain
            return forwardedFor.Split(',')[0].Trim();
        }

        // Check for X-Real-IP header
        var realIp = context.Request.Headers[Headers.RealIp].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // Fallback to direct connection IP
        return context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
    }


    [GeneratedRegex(@"web-app/(?<appVersion>[^\s]+) \((?<browser>[^/]+)/(?<browserVersion>[^;]+); (?<platform>[^;]+); (?<deviceType>[^;]+); (?<screenWidth>\d+)x(?<screenHeight>\d+)(?:; (?<orientation>[^\)]+))?\)")]
    private static partial Regex UserAgentRegex();
}

