using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.API.Store;
using Kavita.Common.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MimeTypes;

namespace Kavita.Server.Controllers;

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
    /// Logger scoped to <see cref="BaseApiController"/>. Available in all derived controllers.
    /// </summary>
    protected ILogger<BaseApiController> Logger =>
        field ??= HttpContext.RequestServices.GetRequiredService<ILogger<BaseApiController>>();

    /// <summary>
    /// Directory service used for temp-file staging helpers. Available in all derived controllers.
    /// </summary>
    protected IDirectoryService DirectoryService =>
        field ??= HttpContext.RequestServices.GetRequiredService<IDirectoryService>();

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

    /// <summary>
    /// Validates that a user-supplied relative path resolves to a location inside the given base directory.
    /// </summary>
    /// <remarks>
    /// This blocks path traversal (e.g. <c>../</c>, absolute/rooted paths) while still permitting nested
    /// relative paths such as <c>.hack/.hack.json</c>. The combined path is canonicalized with
    /// <see cref="Path.GetFullPath(string)"/> (which collapses <c>..</c> segments) and then confirmed to remain
    /// under <paramref name="baseDirectory"/>. Always use the returned/combined path rather than re-joining the
    /// raw input afterwards.
    /// </remarks>
    /// <param name="baseDirectory">The trusted directory the path must stay within.</param>
    /// <param name="relativePath">The untrusted, caller-supplied relative path or filename.</param>
    /// <returns><c>true</c> if <paramref name="relativePath"/> stays within <paramref name="baseDirectory"/>.</returns>
    protected bool IsPathWithinDirectory(string baseDirectory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        // Reject absolute/rooted paths (e.g. "C:\...", "/etc/...", "\\server\share") - the caller owns the base directory
        if (Path.IsPathRooted(relativePath))
        {
            Logger.LogWarning("Rejected rooted path '{RelativePath}' that attempted to escape base directory '{BaseDirectory}'",
                relativePath.Sanitize(), baseDirectory.Sanitize());
            return false;
        }

        var fullBase = Path.GetFullPath(baseDirectory);
        var fullTarget = Path.GetFullPath(Path.Combine(fullBase, relativePath));

        var baseWithSeparator = Path.TrimEndingDirectorySeparator(fullBase) + Path.DirectorySeparatorChar;
        if (!fullTarget.StartsWith(baseWithSeparator, StringComparison.Ordinal))
        {
            Logger.LogWarning("Rejected path traversal attempt: '{RelativePath}' resolved to '{ResolvedPath}' outside base directory '{BaseDirectory}'",
                relativePath.Sanitize(), fullTarget.Sanitize(), fullBase.Sanitize());
            return false;
        }

        return true;
    }

    /// <summary>
    /// Persists an uploaded file into the temp directory and returns the absolute path written.
    /// </summary>
    /// <remarks>
    /// Callers must validate the resulting filename with <see cref="IsPathWithinDirectory"/> before use when the
    /// name originates from untrusted input (e.g. <see cref="IFormFile.FileName"/>).
    /// </remarks>
    /// <param name="file">The uploaded file</param>
    /// <param name="fileName">Override the on-disk filename. Defaults to the uploaded file's name.</param>
    /// <returns>The absolute path of the written temp file</returns>
    protected async Task<string> UploadToTempAsync(IFormFile file, string? fileName = null)
    {
        fileName ??= file.FileName;
        var outputFile = System.IO.Path.Join(DirectoryService.TempDirectory, fileName);

        await using var stream = System.IO.File.Create(outputFile);
        await file.CopyToAsync(stream);
        stream.Close();

        return outputFile;
    }

}
