using System.Threading.Tasks;
using API.Data;
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

        return await System.IO.File.ReadAllTextAsync(themeFile);
    }

    public Task Scan()
    {
        return Task.CompletedTask;
    }
}
