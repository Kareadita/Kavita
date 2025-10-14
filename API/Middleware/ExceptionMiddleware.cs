using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using API.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API.Middleware;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{

    private static readonly JsonSerializerOptions ExceptionJsonSerializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context); // downstream middlewares or http call
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an exception");
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int) HttpStatusCode.InternalServerError;

            var errorMessage = string.IsNullOrEmpty(ex.Message) ? "Internal Server Error" : ex.Message;

            var response = new ApiException(context.Response.StatusCode, errorMessage, ex.StackTrace);

            var json = JsonSerializer.Serialize(response, ExceptionJsonSerializeOptions);

            await context.Response.WriteAsync(json);

        }
    }
}
