using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using API.Errors;
using Kavita.Common;
using Microsoft.AspNetCore.Http;
using Serilog;
using ILogger = Serilog.Core.Logger;

namespace API.Middleware;

public class SecurityEventMiddleware(RequestDelegate next)
{
    private readonly ILogger _logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.File(Path.Join(Directory.GetCurrentDirectory(), "config/logs/", "security.log"), rollingInterval: RollingInterval.Day)
        .CreateLogger();

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (KavitaUnauthenticatedUserException ex)
        {
            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            var requestMethod = context.Request.Method;
            var requestPath = context.Request.Path;
            var userAgent = context.Request.Headers.UserAgent;
            var securityEvent = new
            {
                IpAddress = ipAddress,
                RequestMethod = requestMethod,
                RequestPath = requestPath,
                UserAgent = userAgent,
                CreatedAt = DateTime.Now,
                CreatedAtUtc = DateTime.UtcNow,
            };
            _logger.Information("Unauthorized User attempting to access API. {@Event}", securityEvent);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int) HttpStatusCode.Unauthorized;

            const string errorMessage = "Unauthorized";

            var response = new ApiException(context.Response.StatusCode, errorMessage, ex.StackTrace);

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(response, options);

            await context.Response.WriteAsync(json);
        }
    }
}
