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
    Task Delete(int fontId);
}

public class FontService: IFontService
{

    public static readonly string DefaultFont = "default";

    private readonly IDirectoryService _directoryService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FontService> _logger;

    public FontService(IDirectoryService directoryService, IUnitOfWork unitOfWork, ILogger<FontService> logger)
    {
        _directoryService = directoryService;
        _unitOfWork = unitOfWork;
        _logger = logger;
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

    public async Task Delete(int fontId)
    {
        if (await _unitOfWork.EpubFontRepository.IsFontInUseAsync(fontId))
        {
            throw new KavitaException("errors.delete-font-in-use");
        }

        var font = await _unitOfWork.EpubFontRepository.GetFontAsync(fontId);
        if (font == null)
            return;

        await RemoveFont(font);
    }

    public async Task RemoveFont(EpubFont font)
    {
        if (font.Provider == FontProvider.System)
            return;

        var prefs = await _unitOfWork.UserRepository.GetAllPreferencesByFontAsync(font.Name);
        foreach (var pref in prefs)
        {
            pref.BookReaderFontFamily = DefaultFont;
            _unitOfWork.UserRepository.Update(pref);
        }

        try
        {
            // Copy the theme file to temp for nightly removal (to give user time to reclaim if made a mistake)
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
