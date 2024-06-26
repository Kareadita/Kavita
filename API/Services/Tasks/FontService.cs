using System;
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
    Task<byte[]> GetContent(int fontId);
    Task Scan();
}

public class FontService: IFontService
{

    private readonly IDirectoryService _directoryService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHubContext<MessageHub> _messageHub;
    private readonly ILogger<FontService> _logger;

    public FontService(IDirectoryService directoryService, IUnitOfWork unitOfWork, IHubContext<MessageHub> messageHub,
        ILogger<FontService> logger)
    {
        _directoryService = directoryService;
        _unitOfWork = unitOfWork;
        _messageHub = messageHub;
        _logger = logger;
    }

    public async Task<byte[]> GetContent(int fontId)
    {
        // TODO: Differentiate between Provider.User & Provider.System
        var font = await _unitOfWork.EpubFontRepository.GetFont(fontId);
        if (font == null) throw new KavitaException("Font file missing or invalid");
        var fontFile = _directoryService.FileSystem.Path.Join(_directoryService.EpubFontDirectory, font.FileName);
        if (string.IsNullOrEmpty(fontFile) || !_directoryService.FileSystem.File.Exists(fontFile))
            throw new KavitaException("Font file missing or invalid");
        return await _directoryService.FileSystem.File.ReadAllBytesAsync(fontFile);
    }

    public async Task Scan()
    {
        _directoryService.Exists(_directoryService.EpubFontDirectory);
        var reservedNames = Seed.DefaultFonts.Select(f => f.NormalizedName).ToList();
        var fontFiles =
            _directoryService.GetFilesWithExtension(Parser.NormalizePath(_directoryService.EpubFontDirectory), @"\.[woff2|tff|otf|woff]")
                .Where(name => !reservedNames.Contains(Parser.Normalize(name))).ToList();

        var allFonts = (await _unitOfWork.EpubFontRepository.GetFonts()).ToList();
        var userFonts = allFonts.Where(f => f.Provider == FontProvider.User).ToList();

        foreach (var userFont in userFonts)
        {
            var filePath = Parser.NormalizePath(
                _directoryService.FileSystem.Path.Join(_directoryService.EpubFontDirectory, userFont.FileName));
            if (_directoryService.FileSystem.File.Exists(filePath)) continue;
            allFonts.Remove(userFont);
            await RemoveFont(userFont);

            // TODO: Send update to UI
            _logger.LogInformation("Removed a font because it didn't exist on disk {FilePath}", filePath);
        }

        var allFontNames = allFonts.Select(f => f.NormalizedName).ToList();
        foreach (var fontFile in fontFiles)
        {
            var nakedFileName = _directoryService.FileSystem.Path.GetFileNameWithoutExtension(fontFile);
            // TODO: discuss this, using this to "prettyfy" the file name, to display in the UI
            var fontName = Regex.Replace(nakedFileName, "[^a-zA-Z0-9]", " ");
            var normalizedName = Parser.Normalize(nakedFileName);
            if (allFontNames.Contains(normalizedName)) continue;

            _unitOfWork.EpubFontRepository.Add(new EpubFont()
            {
                Name = fontName,
                NormalizedName = normalizedName,
                FileName = _directoryService.FileSystem.Path.GetFileName(fontFile),
                Provider = FontProvider.User,
            });

            // TODO: Send update to UI
            _logger.LogInformation("Added a new font from disk {FontFile}", fontFile);
        }
        if (_unitOfWork.HasChanges())
        {
            await _unitOfWork.CommitAsync();
        }

        // TODO: Send update to UI
        _logger.LogInformation("Finished FontService#Scan");
    }

    public async Task RemoveFont(EpubFont font)
    {
        // TODO: Default font? Ask in kavita discord if needed, as we can always fallback to the browsers default font.
        _unitOfWork.EpubFontRepository.Remove(font);
        await _unitOfWork.CommitAsync();
    }
}
