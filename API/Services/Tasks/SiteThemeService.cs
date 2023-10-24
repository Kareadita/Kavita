using System;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Entities;
using API.Entities.Enums.Theme;
using API.Extensions;
using API.SignalR;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;

namespace API.Services.Tasks;

public interface IThemeService
{
    Task<string> GetContent(int themeId);
    Task Scan();
    Task UpdateDefault(int themeId);
}

public class ThemeService : IThemeService
{
    private readonly IDirectoryService _directoryService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventHub _eventHub;

    public ThemeService(IDirectoryService directoryService, IUnitOfWork unitOfWork, IEventHub eventHub)
    {
        _directoryService = directoryService;
        _unitOfWork = unitOfWork;
        _eventHub = eventHub;
    }

    /// <summary>
    /// Given a themeId, return the content inside that file
    /// </summary>
    /// <param name="themeId"></param>
    /// <returns></returns>
    public async Task<string> GetContent(int themeId)
    {
        var theme = await _unitOfWork.SiteThemeRepository.GetThemeDto(themeId);
        if (theme == null) throw new KavitaException("theme-doesnt-exist");
        var themeFile = _directoryService.FileSystem.Path.Join(_directoryService.SiteThemeDirectory, theme.FileName);
        if (string.IsNullOrEmpty(themeFile) || !_directoryService.FileSystem.File.Exists(themeFile))
            throw new KavitaException("theme-doesnt-exist");

        return await _directoryService.FileSystem.File.ReadAllTextAsync(themeFile);
    }

    /// <summary>
    /// Scans the site theme directory for custom css files and updates what the system has on store
    /// </summary>
    public async Task Scan()
    {
        _directoryService.ExistOrCreate(_directoryService.SiteThemeDirectory);
        var reservedNames = Seed.DefaultThemes.Select(t => t.NormalizedName).ToList();
        var themeFiles = _directoryService
            .GetFilesWithExtension(Scanner.Parser.Parser.NormalizePath(_directoryService.SiteThemeDirectory), @"\.css")
            .Where(name => !reservedNames.Contains(name.ToNormalized()) && !name.Contains(" "))
            .ToList();

        var allThemes = (await _unitOfWork.SiteThemeRepository.GetThemes()).ToList();

        // First remove any files from allThemes that are User Defined and not on disk
        var userThemes = allThemes.Where(t => t.Provider == ThemeProvider.User).ToList();
        foreach (var userTheme in userThemes)
        {
            var filepath = Scanner.Parser.Parser.NormalizePath(
                _directoryService.FileSystem.Path.Join(_directoryService.SiteThemeDirectory, userTheme.FileName));
            if (_directoryService.FileSystem.File.Exists(filepath)) continue;

            // I need to do the removal different. I need to update all user preferences to use DefaultTheme
            allThemes.Remove(userTheme);
            await RemoveTheme(userTheme);
        }

        // Add new custom themes
        var allThemeNames = allThemes.Select(t => t.NormalizedName).ToList();
        foreach (var themeFile in themeFiles)
        {
            var themeName =
                _directoryService.FileSystem.Path.GetFileNameWithoutExtension(themeFile).ToNormalized();
            if (allThemeNames.Contains(themeName)) continue;

            _unitOfWork.SiteThemeRepository.Add(new SiteTheme()
            {
                Name = _directoryService.FileSystem.Path.GetFileNameWithoutExtension(themeFile),
                NormalizedName = themeName,
                FileName = _directoryService.FileSystem.Path.GetFileName(themeFile),
                Provider = ThemeProvider.User,
                IsDefault = false,
            });

            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
                MessageFactory.SiteThemeProgressEvent(_directoryService.FileSystem.Path.GetFileName(themeFile), themeName,
                    ProgressEventType.Updated));
        }


        if (_unitOfWork.HasChanges())
        {
            await _unitOfWork.CommitAsync();
        }

        // if there are no default themes, reselect Dark as default
        var postSaveThemes = (await _unitOfWork.SiteThemeRepository.GetThemes()).ToList();
        if (!postSaveThemes.Exists(t => t.IsDefault))
        {
            var defaultThemeName = Seed.DefaultThemes.Single(t => t.IsDefault).NormalizedName;
            var theme = postSaveThemes.SingleOrDefault(t => t.NormalizedName == defaultThemeName);
            if (theme != null)
            {
                theme.IsDefault = true;
                _unitOfWork.SiteThemeRepository.Update(theme);
                await _unitOfWork.CommitAsync();
            }

        }

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress,
            MessageFactory.SiteThemeProgressEvent("", "", ProgressEventType.Ended));
    }


    /// <summary>
    /// Removes the theme and any references to it from Pref and sets them to the default at the time.
    /// This commits to DB.
    /// </summary>
    /// <param name="theme"></param>
    private async Task RemoveTheme(SiteTheme theme)
    {
        var prefs = await _unitOfWork.UserRepository.GetAllPreferencesByThemeAsync(theme.Id);
        var defaultTheme = await _unitOfWork.SiteThemeRepository.GetDefaultTheme();
        foreach (var pref in prefs)
        {
            pref.Theme = defaultTheme;
            _unitOfWork.UserRepository.Update(pref);
        }
        _unitOfWork.SiteThemeRepository.Remove(theme);
        await _unitOfWork.CommitAsync();
    }

    /// <summary>
    /// Updates the themeId to the default theme, all others are marked as non-default
    /// </summary>
    /// <param name="themeId"></param>
    /// <returns></returns>
    /// <exception cref="KavitaException">If theme does not exist</exception>
    public async Task UpdateDefault(int themeId)
    {
        try
        {
            var theme = await _unitOfWork.SiteThemeRepository.GetThemeDto(themeId);
            if (theme == null) throw new KavitaException("theme-doesnt-exist");

            foreach (var siteTheme in await _unitOfWork.SiteThemeRepository.GetThemes())
            {
                siteTheme.IsDefault = (siteTheme.Id == themeId);
                _unitOfWork.SiteThemeRepository.Update(siteTheme);
            }

            if (!_unitOfWork.HasChanges()) return;
            await _unitOfWork.CommitAsync();
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }
}
