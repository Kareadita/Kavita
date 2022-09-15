using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;

namespace API.Helpers.Filters;

// NOTE: I'm leaving this in, but I don't think it's needed. Will validate in next release.

//[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
// public class ETagFromFilename : ActionFilterAttribute, IAsyncActionFilter
// {
//     public override async Task OnActionExecutionAsync(ActionExecutingContext executingContext,
//         ActionExecutionDelegate next)
//     {
//         var request = executingContext.HttpContext.Request;
//
//         var executedContext = await next();
//         var response = executedContext.HttpContext.Response;
//
//         // Computing ETags for Response Caching on GET requests
//         if (request.Method == HttpMethod.Get.Method && response.StatusCode == (int) HttpStatusCode.OK)
//         {
//             ValidateETagForResponseCaching(executedContext);
//         }
//     }
//
//     private void ValidateETagForResponseCaching(ActionExecutedContext executedContext)
//     {
//         if (executedContext.Result == null)
//         {
//             return;
//         }
//
//         var request = executedContext.HttpContext.Request;
//         var response = executedContext.HttpContext.Response;
//
//         var objectResult = executedContext.Result as ObjectResult;
//         if (objectResult == null) return;
//         var result = (PhysicalFileResult) objectResult.Value;
//
//         // generate ETag from LastModified property
//         //var etag = GenerateEtagFromFilename(result.);
//
//         // generates ETag from the entire response Content
//         //var etag = GenerateEtagFromResponseBodyWithHash(result);
//
//         if (request.Headers.ContainsKey(HeaderNames.IfNoneMatch))
//         {
//             // fetch etag from the incoming request header
//             var incomingEtag = request.Headers[HeaderNames.IfNoneMatch].ToString();
//
//             // if both the etags are equal
//             // raise a 304 Not Modified Response
//             if (incomingEtag.Equals(etag))
//             {
//                 executedContext.Result = new StatusCodeResult((int) HttpStatusCode.NotModified);
//             }
//         }
//
//         // add ETag response header
//         response.Headers.Add(HeaderNames.ETag, new[] {etag});
//     }
//
     // private static string GenerateEtagFromFilename(HttpResponse response, string filename, int maxAge = 10)
     // {
     //     if (filename is not {Length: > 0}) return string.Empty;
     //     var hashContent = filename + File.GetLastWriteTimeUtc(filename);
     //     using var sha1 = SHA256.Create();
     //     return string.Concat(sha1.ComputeHash(Encoding.UTF8.GetBytes(hashContent)).Select(x => x.ToString("X2")));
     // }
// }

[AttributeUsage(AttributeTargets.Method)]
public class ETagFilter : Attribute, IActionFilter
{
    private readonly int[] _statusCodes;

    public ETagFilter(params int[] statusCodes)
    {
        _statusCodes = statusCodes;
        if (statusCodes.Length == 0) _statusCodes = new[] { 200 };
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        /* Nothing needs to be done here */
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.HttpContext.Request.Method != "GET" || context.HttpContext.Request.Method != "HEAD") return;
        if (!_statusCodes.Contains(context.HttpContext.Response.StatusCode)) return;

        var etag = string.Empty;
        //I just serialize the result to JSON, could do something less costly
        if (context.Result is PhysicalFileResult fileResult)
        {
            // Do a cheap LastWriteTime etag gen
            etag = ETagGenerator.GenerateEtagFromFilename(fileResult.FileName);
            context.HttpContext.Response.Headers.LastModified = File.GetLastWriteTimeUtc(fileResult.FileName).ToLongDateString();
        }

        if (string.IsNullOrEmpty(etag))
        {
            var content = JsonConvert.SerializeObject(context.Result);
            etag = ETagGenerator.GetETag(context.HttpContext.Request.Path.ToString(), Encoding.UTF8.GetBytes(content));
        }


        if (context.HttpContext.Request.Headers.IfNoneMatch.ToString() == etag)
        {
            context.Result = new StatusCodeResult(304);
        }

        //context.HttpContext.Response.Headers.ETag = etag;
    }


}

// Helper class that generates the etag from a key (route) and content (response)
public static class ETagGenerator
{
    public static string GetETag(string key, byte[] contentBytes)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var combinedBytes = Combine(keyBytes, contentBytes);

        return GenerateETag(combinedBytes);
    }

    private static string GenerateETag(byte[] data)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(data);
        var hex = BitConverter.ToString(hash);
        return hex.Replace("-", "");
    }

    private static byte[] Combine(byte[] a, byte[] b)
    {
        var c = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, c, 0, a.Length);
        Buffer.BlockCopy(b, 0, c, a.Length, b.Length);
        return c;
    }

    public static string GenerateEtagFromFilename(string filename)
    {
        if (filename is not {Length: > 0}) return string.Empty;
        var hashContent = filename + File.GetLastWriteTimeUtc(filename);
        using var md5 = MD5.Create();
        return string.Concat(md5.ComputeHash(Encoding.UTF8.GetBytes(hashContent)).Select(x => x.ToString("X2")));
    }
}

// /// <summary>
// /// Enables HTTP Response CacheControl management with ETag values.
// /// </summary>
// public class ClientCacheWithEtagAttribute : ActionFilterAttribute
// {
//     private readonly TimeSpan _clientCache;
//
//     private readonly HttpMethod[] _supportedRequestMethods = {
//         HttpMethod.Get,
//         HttpMethod.Head
//     };
//
//     /// <summary>
//     /// Default constructor
//     /// </summary>
//     /// <param name="clientCacheInSeconds">Indicates for how long the client should cache the response. The value is in seconds</param>
//     public ClientCacheWithEtagAttribute(int clientCacheInSeconds)
//     {
//         _clientCache = TimeSpan.FromSeconds(clientCacheInSeconds);
//     }
//
//     public override async Task OnActionExecutionAsync(ActionExecutingContext executingContext, ActionExecutionDelegate next)
//     {
//
//         if (executingContext.Response?.Content == null)
//         {
//             return;
//         }
//
//         var body = await executingContext.Response.Content.ReadAsStringAsync();
//         if (body == null)
//         {
//             return;
//         }
//
//         var computedEntityTag = GetETag(Encoding.UTF8.GetBytes(body));
//
//         if (actionExecutedContext.Request.Headers.IfNoneMatch.Any()
//             && actionExecutedContext.Request.Headers.IfNoneMatch.First().Tag.Trim('"').Equals(computedEntityTag, StringComparison.InvariantCultureIgnoreCase))
//         {
//             actionExecutedContext.Response.StatusCode = HttpStatusCode.NotModified;
//             actionExecutedContext.Response.Content = null;
//         }
//
//         var cacheControlHeader = new CacheControlHeaderValue
//         {
//             Private = true,
//             MaxAge = _clientCache
//         };
//
//         actionExecutedContext.Response.Headers.ETag = new EntityTagHeaderValue($"\"{computedEntityTag}\"", false);
//         actionExecutedContext.Response.Headers.CacheControl = cacheControlHeader;
//     }
//
//     private static string GetETag(byte[] contentBytes)
//     {
//         using (var md5 = MD5.Create())
//         {
//             var hash = md5.ComputeHash(contentBytes);
//             string hex = BitConverter.ToString(hash);
//             return hex.Replace("-", "");
//         }
//     }
// }

