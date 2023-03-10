using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using API.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;


    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context); // downstream middlewares or http call
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an exception");
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int) HttpStatusCode.InternalServerError;

            var errorMessage = string.IsNullOrEmpty(ex.Message) ? "Internal Server Error" : ex.Message;

            var response = new ApiException(context.Response.StatusCode, errorMessage, ex.StackTrace);

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy =
                    JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(response, options);

            await context.Response.WriteAsync(json);

        }
    }
}
