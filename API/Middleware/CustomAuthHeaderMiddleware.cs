using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using API.Data;
using API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace API.Middleware;

/// <summary>
/// Handles custom headers (like Authentik) which act as a Front door Auth system.
/// This middleware checks against configured IP Addresses and Remote-User header
/// and if matches, attaches Authentication and auto-logs in.
/// </summary>
/// <param name="next"></param>
public class CustomAuthHeaderMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context, IUnitOfWork unitOfWork, ILogger<CustomAuthHeaderMiddleware> logger, ITokenService tokenService)
    {
        // Extract user information from the custom header
        string remoteUser = context.Request.Headers["Remote-User"];
        var isAuthenticated = context.User.Identity is {IsAuthenticated: true};

        // If header missing or user already authenticated, move on
        logger.LogDebug("Remote User: {RemoteUser}, IsAuthenticated: {IsAuthenticated}", remoteUser, isAuthenticated);
        if (string.IsNullOrEmpty(remoteUser) || isAuthenticated)
        {
            await next(context);
            return;
        }

        // Validate IP address
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        var ipAddresses = settings.CustomHeaderWhitelistIpRanges.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (!IsValidIpAddress(context.Connection.RemoteIpAddress, ipAddresses))
        {
            logger.LogWarning("IP ({Ip}) is not whitelisted for custom header login", context.Connection.RemoteIpAddress);
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            await next(context);
            return;
        }


        var user = await unitOfWork.UserRepository.GetUserByEmailAsync(remoteUser);
        if (user == null)
        {
            // Tell security log maybe?
            logger.LogDebug("Ip is whitelisted but user doesn't exist, sending unauthorized");
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            return;
        }

        // Attach the Auth header and allow it to pass through
        var token = await tokenService.CreateToken(user);
        context.Request.Headers.Append("Authorization", $"Bearer {token}");

        // First catch, redirect to login and pre-authenticate
        if (!context.Request.Path.Equals("/login", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogDebug("Redirecting to login with a valid apikey");
            context.Response.Redirect($"/login?apiKey={user.ApiKey}");
            return;
        }

        logger.LogDebug("Auth attached, allowing pass through");
        await next(context);
    }

    private static bool IsValidIpAddress(IPAddress ipAddress, ICollection<string> allowedIpAddresses)
    {
        // Check if the IP address is in the whitelist
        return allowedIpAddresses.Any(ipRange => IpAddressRange.Parse(ipRange).Contains(ipAddress));
    }
}

// Helper class for IP address range parsing
public class IpAddressRange
{
    private readonly uint _startAddress;
    private readonly uint _endAddress;

    private IpAddressRange(uint startAddress, uint endAddress)
    {
        _startAddress = startAddress;
        _endAddress = endAddress;
    }

    public bool Contains(IPAddress address)
    {
        var ipAddressBytes = address.GetAddressBytes();
        var ipAddress = BitConverter.ToUInt32(ipAddressBytes.Reverse().ToArray(), 0);
        return ipAddress >= _startAddress && ipAddress <= _endAddress;
    }

    public static IpAddressRange Parse(string ipRange)
    {
        var parts = ipRange.Split('/');
        var ipAddress = IPAddress.Parse(parts[0]);
        var maskBits = int.Parse(parts[1]);

        var ipBytes = ipAddress.GetAddressBytes().Reverse().ToArray();
        var startAddress = BitConverter.ToUInt32(ipBytes, 0);
        var endAddress = startAddress | (uint.MaxValue >> maskBits);

        return new IpAddressRange(startAddress, endAddress);
    }
}
