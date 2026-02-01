using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using API.Services.Store;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MimeTypes;

namespace API.Controllers;

#nullable enable

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class BaseApiController : ControllerBase
{
    /// <summary>
    /// Gets the current user context. Available in all derived controllers.
    /// </summary>
    protected IUserContext UserContext =>
        field ??= HttpContext.RequestServices.GetRequiredService<IUserContext>();

    /// <summary>
    /// Gets the current authenticated user's ID.
    /// Throws if user is not authenticated.
    /// </summary>
    protected int UserId => UserContext.GetUserIdOrThrow();

    /// <summary>
    /// Gets the current authenticated user's username.
    /// </summary>
    /// <remarks>Warning! Username's can contain .. and /, do not use folders or filenames explicitly with the Username</remarks>
    protected string? Username => UserContext.GetUsername();

    /// <summary>
    /// Returns the auth key used for authentication, null if a different authentication method was used
    /// </summary>
    protected string? AuthKey => User.Claims.FirstOrDefault(c => c.Type == "AuthKey")?.Value;

    /// <summary>
    /// Returns a physical file with proper HTTP caching headers and ETag support.
    /// Automatically handles conditional requests (If-None-Match) returning 304 Not Modified when appropriate.
    /// </summary>
    /// <remarks>This will create a waterfall of cache validation checks if used in virtual scroller</remarks>
    /// <param name="path">The absolute path to the file on disk.</param>
    /// <param name="maxAge">Cache duration in seconds. Default is 300 (5 minutes).</param>
    /// <returns>
    /// <see cref="NotFoundResult"/> if path is null/empty or file doesn't exist.
    /// <see cref="StatusCodeResult"/> with 304 if client's cached version is current.
    /// <see cref="PhysicalFileResult"/> with the file content and caching headers otherwise.
    /// </returns>
    protected ActionResult CachedFile(string? path, int maxAge = 300)
    {
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return NotFound();

        var lastWrite = System.IO.File.GetLastWriteTimeUtc(path);
        var etag = $"\"{lastWrite.Ticks:x}-{path.GetHashCode():x}\"";

        if (Request.Headers.IfNoneMatch.Any(t => t == etag)) return StatusCode(304);

        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = $"private, max-age={maxAge}, stale-while-revalidate={maxAge}";

        var contentType = MimeTypeMap.GetMimeType(Path.GetExtension(path));
        return PhysicalFile(path, contentType, Path.GetFileName(path), enableRangeProcessing: true);
    }

    /// <summary>
    /// Returns a physical file
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    protected ActionResult PhysicalFile(string? path)
    {
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return NotFound();

        var contentType = MimeTypeMap.GetMimeType(Path.GetExtension(path));
        return PhysicalFile(path, contentType, Path.GetFileName(path), enableRangeProcessing: true);
    }

    /// <summary>
    /// Returns a file from byte[] content with proper HTTP caching headers and ETag support.
    /// ETag is generated from SHA256 hash of the content.
    /// Automatically handles conditional requests (If-None-Match) returning 304 Not Modified when appropriate.
    /// </summary>
    /// <param name="content">The file content as byte array.</param>
    /// <param name="contentType">The MIME type of the content.</param>
    /// <param name="fileName">Optional filename for Content-Disposition header.</param>
    /// <param name="maxAge">Cache duration in seconds. Default is 86400 (1 day).</param>
    /// <returns>
    /// <see cref="NotFoundResult"/> if content is null or empty.
    /// <see cref="StatusCodeResult"/> with 304 if client's cached version is current.
    /// <see cref="FileContentResult"/> with the content and caching headers otherwise.
    /// </returns>
    protected ActionResult CachedContent(byte[]? content, string contentType, string? fileName = null, int maxAge = 86400)
    {
        if (content is not { Length: > 0 })
            return NotFound();

        var etag = $"\"{Convert.ToHexString(SHA256.HashData(content))}\"";

        if (Request.Headers.IfNoneMatch.ToString() == etag)
            return StatusCode(304);

        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = $"private, max-age={maxAge}";

        return fileName is not null
            ? File(content, contentType, fileName)
            : File(content, contentType);
    }

}
