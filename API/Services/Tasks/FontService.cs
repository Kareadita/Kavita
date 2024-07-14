using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using API.Data;
using API.Entities;
using API.Entities.Enums.Font;
using API.Services.Tasks.Scanner.Parser;
using API.SignalR;
using Kavita.Common;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks;

public interface IFontService
{
    Task<EpubFont> CreateFontFromFileAsync(string path);
    Task Delete(int fontId, bool force);
    Task CreateFontFromUrl(string url);
}

public class FontService: IFontService
{

    public static readonly string DefaultFont = "default";

    private readonly IDirectoryService _directoryService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FontService> _logger;
    private readonly IEventHub _eventHub;

    private const string SupportedFontUrlPrefix = "https://fonts.google.com/specimen/";

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

    public async Task Delete(int fontId, bool force)
    {
        if (!force && await _unitOfWork.EpubFontRepository.IsFontInUseAsync(fontId))
        {
            throw new KavitaException("errors.delete-font-in-use");
        }

        var font = await _unitOfWork.EpubFontRepository.GetFontAsync(fontId);
        if (font == null) return;

        if (font.Provider == FontProvider.System)
        {
            throw new KavitaException("errors.cant-delete-system-font");
        }

        await RemoveFont(font);
    }

    public Task CreateFontFromUrl(string url)
    {
        if (!url.StartsWith(SupportedFontUrlPrefix))
        {
            throw new KavitaException("font-url-not-allowed");
        }

        // Extract Font name from url
        var fontFamily = url.Split(SupportedFontUrlPrefix)[1].Split("?")[0];
        _logger.LogInformation("Preparing to download {FontName} font", fontFamily);

        // TODO: Send a font update event
        return Task.CompletedTask;
    }

    private async Task RemoveFont(EpubFont font)
    {
        if (font.Provider == FontProvider.System) return;

        var prefs = await _unitOfWork.UserRepository.GetAllPreferencesByFontAsync(font.Name);
        foreach (var pref in prefs)
        {
            // TODO: SignalR message informing your front has been reset to the default font
            pref.BookReaderFontFamily = DefaultFont;
            _unitOfWork.UserRepository.Update(pref);
        }

        try
        {
            // Copy the font file to temp for nightly removal (to give user time to reclaim if made a mistake)
            var existingLocation =
                _directoryService.FileSystem.Path.Join(_directoryService.SiteThemeDirectory, font.FileName);
            var newLocation =
                _directoryService.FileSystem.Path.Join(_directoryService.TempDirectory, font.FileName);
            _directoryService.CopyFileToDirectory(existingLocation, newLocation);
            _directoryService.DeleteFiles([existingLocation]);
        }
        catch (Exception) { /* Swallow */ }

        _unitOfWork.EpubFontRepository.Remove(font);
        await _unitOfWork.CommitAsync();
    }
}
