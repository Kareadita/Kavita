using System;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace API.Middleware;

public class SecurityEventMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityEventMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString();
        var requestMethod = context.Request.Method;
        var requestPath = context.Request.Path;
        var userAgent = context.Request.Headers["User-Agent"];

        var securityEvent = new SecurityEvent
        {
            IpAddress = ipAddress,
            RequestMethod = requestMethod,
            RequestPath = requestPath,
            UserAgent = userAgent,
            CreatedAt = DateTime.Now,
            CreatedAtUtc = DateTime.UtcNow,
        };

        using (var scope = context.RequestServices.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SecurityEventMiddleware>>();
            dbContext.Add(securityEvent);
            await dbContext.SaveChangesAsync();
            logger.LogDebug("Request Processed: {@SecurityEvent}", securityEvent);
        }


        await _next(context);
    }
}
