using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.Common.Extensions;
using Kavita.Models.Constants;
using Kavita.Services.Scanner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.Controllers;

/// <summary>
/// Iterverse addition: lets admins upload books straight into a library folder from the web UI.
/// Kept self-contained (no edits to upstream controllers/constants) so upstream merges stay clean.
/// </summary>
[Authorize(Policy = PolicyGroups.AdminPolicy)]
public class BookUploadController(
    IUnitOfWork unitOfWork,
    IDirectoryService directoryService,
    ILogger<BookUploadController> logger) : BaseApiController
{
    /// <summary>
    /// Max size of a single uploaded file. Note: a proxy in front of Kavita (e.g. Cloudflare) may enforce a lower cap.
    /// </summary>
    private const long MaxBookUploadSizeBytes = 1_073_741_824; // 1 GB

    private static readonly char[] InvalidNameChars =
        [.. Path.GetInvalidFileNameChars(), '/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    /// <summary>
    /// Saves one book/archive into <c>{folderPath}/{seriesFolder}/</c> of a library. Does not trigger a scan; the
    /// client should scan the library once its batch of uploads finishes.
    /// </summary>
    /// <param name="libraryId">Target library</param>
    /// <param name="folderPath">One of the library's root folders, exactly as returned by the library API</param>
    /// <param name="seriesFolder">Sub-folder to place the file in (Kavita expects files inside a series folder)</param>
    /// <param name="file">The book to upload</param>
    /// <returns>The path the file was written to, relative to the library root folder</returns>
    [HttpPost]
    [RequestSizeLimit(MaxBookUploadSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxBookUploadSizeBytes)]
    public async Task<ActionResult<string>> Upload([FromQuery] int libraryId, [FromQuery] string folderPath,
        [FromQuery] string seriesFolder, IFormFile file)
    {
        var ct = HttpContext.RequestAborted;
        if (file.Length == 0) return BadRequest("The file is empty");

        var library = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId, LibraryIncludes.Folders, ct);
        if (library == null) return BadRequest("Library does not exist");

        var normalizedFolder = Parser.NormalizePath(folderPath);
        var root = library.Folders.Select(f => f.Path).FirstOrDefault(p => Parser.NormalizePath(p) == normalizedFolder);
        if (root == null) return BadRequest("Folder does not belong to this library");

        var fileName = SanitizeName(Path.GetFileName(file.FileName));
        var folderName = SanitizeName(seriesFolder);
        if (fileName == null || folderName == null) return BadRequest("Invalid file or folder name");

        if (!Parser.IsArchive(fileName) && !Parser.IsBook(fileName))
        {
            return BadRequest("This file type is not supported. Upload an epub, pdf, or comic archive");
        }

        var fs = directoryService.FileSystem;
        var rootFull = fs.Path.GetFullPath(root);
        var targetDir = fs.Path.GetFullPath(fs.Path.Combine(rootFull, folderName));
        var targetFile = fs.Path.Combine(targetDir, fileName);

        // Defense in depth: sanitisation above should already make escaping the root impossible
        if (!targetDir.StartsWith(rootFull.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return BadRequest("Invalid folder");
        }

        if (fs.File.Exists(targetFile)) return Conflict("A file with this name already exists in that folder");

        // Write under a non-book extension first so the folder watcher/scanner never sees a half-written file
        var partialFile = targetFile + ".uploading";
        try
        {
            fs.Directory.CreateDirectory(targetDir);
            await using (var stream = fs.File.Create(partialFile))
            {
                await file.CopyToAsync(stream, ct);
            }
            fs.File.Move(partialFile, targetFile);
        }
        catch (Exception ex)
        {
            if (fs.File.Exists(partialFile)) fs.File.Delete(partialFile);
            if (ex is OperationCanceledException) throw;
            logger.LogError(ex, "Failed to save uploaded book {FileName} to {Folder}", fileName.Sanitize(), targetDir.Sanitize());
            return BadRequest("Could not save the file on the server");
        }

        logger.LogInformation("Admin uploaded {FileName} into library {LibraryName} at {Folder}",
            fileName.Sanitize(), library.Name.Sanitize(), targetDir.Sanitize());

        return Ok(fs.Path.Combine(folderName, fileName));
    }

    /// <summary>
    /// Returns a single safe path segment, or null if nothing usable remains.
    /// </summary>
    private static string? SanitizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var cleaned = new string(name.Select(c => InvalidNameChars.Contains(c) || char.IsControl(c) ? '_' : c).ToArray())
            .Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(cleaned) || cleaned == "." || cleaned == ".." ? null : cleaned;
    }
}
