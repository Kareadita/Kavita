using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Entities;
using API.Entities.Enums.Theme;
using API.SignalR;
using Kavita.Common;
using Microsoft.AspNetCore.SignalR;

namespace API.Services.Tasks;

public interface ISiteThemeService
{
    Task<string> GetContent(int themeId);
    Task Scan();
}

public class SiteThemeService : ISiteThemeService
{
    private readonly IDirectoryService _directoryService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHubContext<MessageHub> _messageHub;

    public SiteThemeService(IDirectoryService directoryService, IUnitOfWork unitOfWork, IHubContext<MessageHub> messageHub)
    {
        _directoryService = directoryService;
        _unitOfWork = unitOfWork;
        _messageHub = messageHub;
    }

    public async Task<string> GetContent(int themeId)
    {
        var theme = await _unitOfWork.SiteThemeRepository.GetThemeDto(themeId);
        var themeFile = _directoryService.FileSystem.Path.Join(_directoryService.SiteThemeDirectory, theme.FileName);
        if (string.IsNullOrEmpty(themeFile) || !_directoryService.FileSystem.File.Exists(themeFile))
            throw new KavitaException("Theme file missing or invalid");

        return await _directoryService.FileSystem.File.ReadAllTextAsync(themeFile);
    }

    public async Task Scan()
    {
        _directoryService.ExistOrCreate(_directoryService.SiteThemeDirectory);
        var reservedNames = Seed.DefaultThemes.Select(t => t.Name.ToLower()).ToList();
        var themeFiles = _directoryService.GetFilesWithExtension(Parser.Parser.NormalizePath(_directoryService.SiteThemeDirectory), @"\.css")
            .Where(name => !reservedNames.Contains(name.ToLower()));

        var allThemes = (await _unitOfWork.SiteThemeRepository.GetThemes());
        var allThemeNames = allThemes.Select(t => t.Name.ToLower()).ToList();
        foreach (var themeFile in themeFiles)
        {
            if (allThemeNames.Contains(themeFile)) continue;
            _unitOfWork.SiteThemeRepository.Add(new SiteTheme()
            {
                Name = _directoryService.FileSystem.Path.GetFileNameWithoutExtension(themeFile),
                FileName = themeFile,
                Provider = ThemeProvider.User,
                IsDefault = false
            });
        }
        // TODO: Need to be able delete old entries

        if (_unitOfWork.HasChanges() && await _unitOfWork.CommitAsync())
        {
            // TODO: Emit an event that Scan has completed to theme cache can be updated
            //_messageHub.
        }

    }
}
