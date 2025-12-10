using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using API.Data;
using API.Entities;
using API.Entities.Enums.Font;
using API.Extensions;
using API.Services.Tasks.Scanner.Parser;
using API.SignalR;
using Flurl.Http;
using Kavita.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace API.Services.Tasks;

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
    /// <returns>GoogleFontsFileRef</returns>
    public GoogleFontsFileRef? VariableFont()
    {
        foreach (var fileRef in manifest.fileRefs)
        {
            // Filename prefixed with static means it's a Bold/Italic/... font
            if (!fileRef.filename.StartsWith("static/"))
            {
                return fileRef;
            }
        }

        return null;
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

public interface IFontService
{
    Task<EpubFont> CreateFontFromFileAsync(string path);
    Task Delete(int fontId);
    Task<EpubFont> CreateFontFromUrl(string url);
    Task<bool> IsFontInUse(int fontId);
}

public class FontService: IFontService
{

    public static readonly string DefaultFont = "Default";

    private readonly IDirectoryService _directoryService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FontService> _logger;
    private readonly IEventHub _eventHub;

    private const string SupportedFontUrlPrefix = "https://fonts.google.com/";
    private const string DownloadFontUrlPrefix = "https://fonts.google.com/download/list?family=";
    private const string GoogleFontsInvalidJsonPrefix = ")]}'";

    public FontService(IDirectoryService directoryService, IUnitOfWork unitOfWork, ILogger<FontService> logger, IEventHub eventHub)
    {
        _directoryService = directoryService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _eventHub = eventHub;
    }

    public async Task<EpubFont> CreateFontFromFileAsync(string path)
    {
        if (!_directoryService.FileSystem.File.Exists(path))
        {
            _logger.LogInformation("Unable to create font from manual upload as font not in temp");
            throw new KavitaException("errors.font-manual-upload");
        }

        var fileName = _directoryService.FileSystem.FileInfo.New(path).Name;
        var nakedFileName = _directoryService.FileSystem.Path.GetFileNameWithoutExtension(fileName);
        var fontName = Parser.PrettifyFileName(nakedFileName);
        var normalizedName = Parser.Normalize(nakedFileName);

        if (await _unitOfWork.EpubFontRepository.GetFontDtoByNameAsync(fontName) != null)
        {
            throw new KavitaException("errors.font-already-in-use");
        }

        _directoryService.CopyFileToDirectory(path, _directoryService.EpubFontDirectory);
        var finalLocation = _directoryService.FileSystem.Path.Join(_directoryService.EpubFontDirectory, fileName);

        var font = new EpubFont()
        {
            Name = fontName,
            NormalizedName = normalizedName,
            FileName = Path.GetFileName(finalLocation),
            Provider = FontProvider.User
        };
        _unitOfWork.EpubFontRepository.Add(font);
        await _unitOfWork.CommitAsync();

        // TODO: Send update to UI
        return font;
    }

    /// <summary>
    /// This does not check if in use, use <see cref="IsFontInUse"/>
    /// </summary>
    /// <param name="fontId"></param>
    public async Task Delete(int fontId)
    {
        var font = await _unitOfWork.EpubFontRepository.GetFontAsync(fontId);
        if (font == null) return;

        await RemoveFont(font);
    }

    public async Task<EpubFont> CreateFontFromUrl(string url)
    {
        if (!url.StartsWith(SupportedFontUrlPrefix))
        {
            throw new KavitaException("font-url-not-allowed");
        }

        // Extract Font name from url
        var fontFamily = url.Split(SupportedFontUrlPrefix)[1].Split("?")[0].Split("/").Last();
        _logger.LogInformation("Preparing to download {FontName} font", fontFamily.Sanitize());

        var metaData = await GetGoogleFontsMetadataAsync(fontFamily);
        if (metaData == null)
        {
            _logger.LogError("Unable to find metadata for {FontName}", fontFamily.Sanitize());
            throw new KavitaException("errors.font-not-found");
        }

        var googleFontRef = metaData.VariableFont();
        if (googleFontRef == null)
        {
            _logger.LogError("Unable to find variable font for {FontName} with metadata {MetaData}", fontFamily.Sanitize(), metaData);
            throw new KavitaException("errors.font-not-found");
        }

        var fontExt = Path.GetExtension(googleFontRef.filename);
        var fileName = $"{fontFamily}{fontExt}";

        _logger.LogDebug("Downloading font {FontFamily} to {FileName} from {Url}", fontFamily.Sanitize(), fileName, googleFontRef.url);
        var path = await googleFontRef.url.DownloadFileAsync(_directoryService.TempDirectory, fileName);

        return await CreateFontFromFileAsync(path);
    }

    /// <summary>
    /// Returns if the given font is in use by any other user. System provided fonts will always return true.
    /// </summary>
    /// <param name="fontId"></param>
    /// <returns></returns>
    public async Task<bool> IsFontInUse(int fontId)
    {
        var font = await _unitOfWork.EpubFontRepository.GetFontAsync(fontId);
        if (font == null || font.Provider == FontProvider.System) return true;

        return await _unitOfWork.EpubFontRepository.IsFontInUseAsync(fontId);
    }

    public async Task RemoveFont(EpubFont font)
    {
        if (font.Provider == FontProvider.System) return;

        var prefs = await _unitOfWork.UserRepository.GetAllPreferencesByFontAsync(font.Name);
        foreach (var pref in prefs)
        {
            pref.BookReaderFontFamily = DefaultFont;
            _unitOfWork.UserRepository.Update(pref);
        }

        try
        {
            // Copy the font file to temp for nightly removal (to give user time to reclaim if made a mistake)
            var existingLocation =
                _directoryService.FileSystem.Path.Join(_directoryService.EpubFontDirectory, font.FileName);
            var newLocation =
                _directoryService.FileSystem.Path.Join(_directoryService.TempDirectory, font.FileName);
            _directoryService.CopyFileToDirectory(existingLocation, newLocation);
            _directoryService.DeleteFiles([existingLocation]);
        }
        catch (Exception) { /* Swallow */ }

        _unitOfWork.EpubFontRepository.Remove(font);
        await _unitOfWork.CommitAsync();
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
            _logger.LogError(ex, "Unable to get metadata for {FontName} from {Url}", fontName.Sanitize(), url);
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
