using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Models;
using Kavita.Models.DTOs.Font;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums.Font;
using Kavita.Services.Scanner;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Kavita.Services;

// Although we don't use all the fields, just including them all for completeness
internal class GoogleFontsMetadata
{
    /// <summary>
    /// Name of the zip file container all fonts
    /// </summary>
    public required string zipName { get; init; }

    /// <summary>
    /// Manifest, information about the content of the zip
    /// </summary>
    public required GoogleFontsManifest manifest { get; init; }

    /// <summary>
    /// Tries to find the variable font in the manifest
    /// </summary>
    /// <returns>GoogleFontsFileRef[]</returns>
    public GoogleFontsFileRef[] VariableFont()
    {
        return Array.FindAll(manifest.fileRefs, fontRefs => fontRefs.filename.Contains("variable", StringComparison.OrdinalIgnoreCase));
    }
}

internal class GoogleFontsManifest
{
    /// <summary>
    /// Files included in the zip
    /// <example>README.txt</example>
    /// </summary>
    public required GoogleFontsFile[] files { get; init; }
    /// <summary>
    /// References to the actual fonts
    /// </summary>
    public required GoogleFontsFileRef[] fileRefs { get; init; }
}

internal class GoogleFontsFile
{
    public required string filename { get; init; }
    public required string contents { get; init; }
}

internal class GoogleFontsFileRef
{
    public required string filename { get; init; }
    public required string url { get; init; }
    public required GoogleFontsData date { get; init; }
}

internal class GoogleFontsData
{
    public required int seconds { get; init; }
    public required int nanos { get; init; }
}

public class FontService(IDirectoryService directoryService, IUnitOfWork unitOfWork, ILogger<FontService> logger,
    IUrlValidationService urlValidationService)
    : IFontService
{
    private const string SupportedFontUrlPrefix = "https://fonts.google.com/";
    private const string DownloadFontUrlPrefix = "https://fonts.google.com/download/list?family=";
    private const string GoogleFontsInvalidJsonPrefix = ")]}'";


    public async Task<EpubFont> CreateFontFromFileAsync(string path, CancellationToken ct = default)
    {
        // Validate that font was uploaded
        if (!directoryService.FileSystem.File.Exists(path))
        {
            logger.LogInformation("Unable to create font from manual upload as font not in temp");
            throw new KavitaException("errors.font-manual-upload");
        }

        // Extract filename and then parse font object from the filename
        var fileName = directoryService.FileSystem.FileInfo.New(path).Name;
        var font = Parser.ParseEpubFontFromFilename(fileName);

        var dbFont = await unitOfWork.EpubFontRepository.GetFontByNameAsync(font.Name, ct);

        // Check if font name is already present in database
        // If it is, return the one that is already in the database.
        if (dbFont != null)
        {
            return dbFont;
        }

        // Copy file from temp directory to EpubFontDirectory
        directoryService.CopyFileToDirectory(path, directoryService.EpubFontDirectory);

        // Commit font to database
        unitOfWork.EpubFontRepository.Add(font);
        await unitOfWork.CommitAsync(ct);

        // default: Send update to UI
        return font;
    }

    /// <summary>
    /// Deletes the entire font family that the given font belongs to. Rejects when in-use without <c>forceDelete</c>
    /// </summary>
    /// <param name="fontId">Any file within the family to delete</param>
    /// <param name="forceDelete">Delete even when the family is currently in use</param>
    /// <param name="ct"></param>
    public async Task<FontDeleteResultDto> DeleteFamily(int fontId, bool forceDelete, CancellationToken ct = default)
    {
        var font = await unitOfWork.EpubFontRepository.GetFontAsync(fontId, ct);
        if (font == null || font.Provider == FontProvider.System)
        {
            return new FontDeleteResultDto {Deleted = false, InUse = false};
        }

        var family = font.Family;
        var inUse = await unitOfWork.EpubFontRepository.IsFontFamilyInUseAsync(family, ct);
        if (inUse && !forceDelete)
        {
            return new FontDeleteResultDto {Deleted = false, InUse = true};
        }

        // Only user provided files can be removed; system fonts in the family (if any) are left untouched
        var files = (await unitOfWork.EpubFontRepository.GetFontsByFamilyAsync(family, ct))
            .Where(f => f.Provider == FontProvider.User)
            .ToList();
        if (files.Count == 0)
        {
            return new FontDeleteResultDto {Deleted = false, InUse = inUse};
        }

        await ResetFamilyReferences(family);

        foreach (var file in files)
        {
            MoveFontFileToTemp(file);
            unitOfWork.EpubFontRepository.Remove(file);
        }

        await unitOfWork.CommitAsync(ct);

        return new FontDeleteResultDto {Deleted = true, InUse = inUse};
    }

    public async Task<EpubFont[]> CreateFontsFromUrl(string url, CancellationToken ct = default)
    {
        if (!url.StartsWith(SupportedFontUrlPrefix))
        {
            throw new KavitaException("font-url-not-allowed");
        }

        // Extract Font name from url
        var fontFamily = url.Split(SupportedFontUrlPrefix)[1].Split("?")[0].Split("/").Last();
        logger.LogInformation("Preparing to download {FontName} font", fontFamily.Sanitize());

        var metaData = await GetGoogleFontsMetadataAsync(fontFamily);
        if (metaData == null)
        {
            logger.LogError("Unable to find metadata for {FontName}", fontFamily.Sanitize());
            throw new KavitaException("errors.font-not-found");
        }

        // Choose the variable font if available
        // Otherwise take the full list.
        // This should be fine since Google Fonts seems to
        // only prepend filenames with 'static/' for font
        // families that have variable fonts since the
        // static/ path contains non-variable variants of
        // the font for applications that don't support
        // variable fonts.
        var googleFontRefs = metaData.VariableFont();
        if (googleFontRefs.Length == 0)
        {
            googleFontRefs = metaData.manifest.fileRefs;
        }

        var finalRef = new List<EpubFont>();
        foreach (var fontRef in googleFontRefs)
        {
            await urlValidationService.ValidateUrlAsync(fontRef.url);

            // filename comes from remote metadata and may contain path separators (e.g. static/...).
            // Reduce it to a basename so the download can't create unexpected subdirectories or traverse outside temp.
            var fileName = directoryService.FileSystem.Path.GetFileName(fontRef.filename.Replace('\\', '/'));

            logger.LogDebug("Downloading font {FontFamily} to {FileName} from {Url}", fontFamily.Sanitize(), fileName.Sanitize(), fontRef.url);
            var path = await fontRef.url.DownloadFileAsync(directoryService.TempDirectory, fileName, cancellationToken: ct);

            var result = await CreateFontFromFileAsync(path, ct);
            finalRef.Add(result);
        }

        return [..finalRef];
    }

    /// <summary>
    /// Points every user selecting this family back at the default font. Reading profiles are the live source the
    /// reader reads from; the legacy preferences are reset too so no stale references are left behind.
    /// </summary>
    private async Task ResetFamilyReferences(string family)
    {
        var profiles = await unitOfWork.AppUserReadingProfileRepository.GetProfilesByFontFamily(family);
        foreach (var profile in profiles)
        {
            profile.BookReaderFontFamily = Defaults.DefaultFont;
            unitOfWork.AppUserReadingProfileRepository.Update(profile);
        }
    }

    private void MoveFontFileToTemp(EpubFont font)
    {
        try
        {
            // Copy the font file to temp for nightly removal (to give user time to reclaim if made a mistake)
            var existingLocation =
                directoryService.FileSystem.Path.Join(directoryService.EpubFontDirectory, font.FileName);
            var newLocation =
                directoryService.FileSystem.Path.Join(directoryService.TempDirectory, font.FileName);
            directoryService.CopyFileToDirectory(existingLocation, newLocation);
            directoryService.DeleteFiles([existingLocation]);
        }
        catch (Exception) { /* Swallow */ }
    }

    private async Task<GoogleFontsMetadata?> GetGoogleFontsMetadataAsync(string fontName)
    {
        var url = DownloadFontUrlPrefix + fontName;
        string content;

        // The request may fail if the users URL is invalid or the font doesn't exist
        // The error this produces is ugly and not user-friendly, so we catch it here
        try
        {
            content = await url
                .WithHeader(HeaderNames.Accept, "application/json")
                .WithHeader(HeaderNames.UserAgent, "Kavita")
                .GetStringAsync();
        } catch (Exception ex)
        {
            logger.LogError(ex, "Unable to get metadata for {FontName} from {Url}", fontName.Sanitize(), url.Sanitize());
            return null;
        }

        // The returned response isn't valid json and has this weird prefix, removing it here...
        if (content.StartsWith(GoogleFontsInvalidJsonPrefix))
        {
            content = content[GoogleFontsInvalidJsonPrefix.Length..];
        }

        return JsonSerializer.Deserialize<GoogleFontsMetadata>(content);
    }


}
