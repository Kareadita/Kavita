using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Kavita.API.Errors;
using Kavita.Common;
using Kavita.Database.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.Middleware;

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
        catch (TaskCanceledException ex)
        {
            logger.LogTrace(ex, "Request was cancelled");
            context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
            await context.Response.CompleteAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogDbUpdateConcurrencyException(ex);
            await WriteException(context, ex);
        }
        catch (Exception ex) when (ex is KavitaUnauthenticatedUserException or UnauthorizedAccessException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            await context.Response.CompleteAsync();
        }
        catch (Exception ex) when (ex is KavitaNotFoundException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            await context.Response.CompleteAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an exception");
            await WriteException(context, ex);
        }
    }

    private async Task WriteException(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int) HttpStatusCode.InternalServerError;

        var errorMessage = string.IsNullOrEmpty(ex.Message) ? "Internal Server Error" : ex.Message;

        var response = new ApiException(context.Response.StatusCode, errorMessage, ex.StackTrace);

        var json = JsonSerializer.Serialize(response, ExceptionJsonSerializeOptions);

        await context.Response.WriteAsync(json);
    }
}
