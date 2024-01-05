using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using API.Data;
using API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace API.Middleware;

public class CustomAuthHeaderMiddleware(RequestDelegate next)
{
    // Hardcoded list of allowed IP addresses in CIDR format
    private readonly string[] allowedIpAddresses = { "192.168.1.0/24", "2001:db8::/32" };


    public async Task Invoke(HttpContext context, IUnitOfWork unitOfWork)
    {
        // Extract user information from the custom header
        string remoteUser = context.Request.Headers["Remote-User"];

        // If header missing or user already authenticated, move on
        if (string.IsNullOrEmpty(remoteUser) || context.User.Identity is {IsAuthenticated: true})
        {
            await next(context);
            return;
        }



        // Validate IP address
        if (IsValidIpAddress(context.Connection.RemoteIpAddress))
        {
            // Perform additional authentication logic if needed
            // For now, you can log the authenticated user
            var user = await unitOfWork.UserRepository.GetUserByEmailAsync(remoteUser);
            if (user == null)
            {
                // Tell security log maybe?
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return;
            }
            // Check if the RemoteUser has an account on the server
            if (!context.Request.Path.Equals("/login", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Redirect($"/login?apiKey={user.ApiKey}");
                return;
            }
        }
        await next(context);
    }

    private bool IsValidIpAddress(IPAddress ipAddress)
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
