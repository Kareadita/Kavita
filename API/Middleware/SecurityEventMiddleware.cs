using System;
using System.IO;
using System.Security.AccessControl;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Entities;
using API.Logging;
using API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using ILogger = Serilog.ILogger;

namespace API.Middleware;

public class SecurityEventMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger _logger;

    public SecurityEventMiddleware(RequestDelegate next)
    {
        _next = next;

        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(Path.Join(Directory.GetCurrentDirectory(), "config/logs/", "security.log"), rollingInterval: RollingInterval.Day)
            .CreateLogger();
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
            dbContext.Add(securityEvent);
            await dbContext.SaveChangesAsync();
            _logger.Debug("Request Processed: {@SecurityEvent}", securityEvent);
        }


        await _next(context);
    }
}
