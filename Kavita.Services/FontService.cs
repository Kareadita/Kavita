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

        // Check if font name is already present in database
        if (await unitOfWork.EpubFontRepository.GetFontDtoByNameAsync(font.Name, ct) != null)
        {
            throw new KavitaException("errors.font-already-in-use");
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
    /// This does not check if in use, use <see cref="IsFontInUse"/>
    /// </summary>
    /// <param name="fontId"></param>
    /// <param name="ct"></param>
    public async Task Delete(int fontId, CancellationToken ct = default)
    {
        var font = await unitOfWork.EpubFontRepository.GetFontAsync(fontId, ct);
        if (font == null) return;

        await RemoveFont(font);
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

        var googleFontRefs = metaData.VariableFont();
        if (googleFontRefs.Length == 0)
        {
            logger.LogError("Unable to find variable font for {FontName} with metadata {MetaData}", fontFamily.Sanitize(), metaData);
            throw new KavitaException("errors.font-not-found");
        }

        var finalRef = new List<EpubFont>();
        foreach (var fontRef in googleFontRefs)
        {
            await urlValidationService.ValidateUrlAsync(fontRef.url);
            logger.LogDebug("Downloading font {FontFamily} to {FileName} from {Url}", fontFamily.Sanitize(), fontRef.filename.Sanitize(), fontRef.url);
            var path = await fontRef.url.DownloadFileAsync(directoryService.TempDirectory, fontRef.filename, cancellationToken: ct);

            var result = await CreateFontFromFileAsync(path, ct);
            finalRef.Add(result);
        }

        return [..finalRef];
    }

    /// <summary>
    /// Returns if the given font is in use by any other user. System provided fonts will always return true.
    /// </summary>
    /// <param name="fontId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<bool> IsFontInUse(int fontId, CancellationToken ct = default)
    {
        var font = await unitOfWork.EpubFontRepository.GetFontAsync(fontId, ct);
        if (font == null || font.Provider == FontProvider.System) return true;

        return await unitOfWork.EpubFontRepository.IsFontInUseAsync(fontId, ct);
    }

    private async Task RemoveFont(EpubFont font)
    {
        if (font.Provider == FontProvider.System) return;

        var prefs = await unitOfWork.UserRepository.GetAllPreferencesByFontAsync(font.Family);
        foreach (var pref in prefs)
        {
            pref.BookReaderFontFamily = Defaults.DefaultFont;
            unitOfWork.UserRepository.Update(pref);
        }

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

        unitOfWork.EpubFontRepository.Remove(font);
        await unitOfWork.CommitAsync();
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
