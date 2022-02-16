﻿using System;
using System.Collections.Generic;
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
    Task UpdateDefault(int themeId);
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

    /// <summary>
    /// Given a themeId, return the content inside that file
    /// </summary>
    /// <param name="themeId"></param>
    /// <returns></returns>
    /// <exception cref="KavitaException"></exception>
    public async Task<string> GetContent(int themeId)
    {
        var theme = await _unitOfWork.SiteThemeRepository.GetThemeDto(themeId);
        if (theme == null) throw new KavitaException("Theme file missing or invalid");
        var themeFile = _directoryService.FileSystem.Path.Join(_directoryService.SiteThemeDirectory, theme.FileName);
        if (string.IsNullOrEmpty(themeFile) || !_directoryService.FileSystem.File.Exists(themeFile))
            throw new KavitaException("Theme file missing or invalid");

        return await _directoryService.FileSystem.File.ReadAllTextAsync(themeFile);
    }

    /// <summary>
    /// Scans the site theme directory for custom css files and updates what the system has on store
    /// </summary>
    public async Task Scan()
    {
        _directoryService.ExistOrCreate(_directoryService.SiteThemeDirectory);
        var reservedNames = Seed.DefaultThemes.Select(t => t.NormalizedName).ToList();
        var themeFiles = _directoryService.GetFilesWithExtension(Parser.Parser.NormalizePath(_directoryService.SiteThemeDirectory), @"\.css")
            .Where(name => !reservedNames.Contains(Parser.Parser.Normalize(name))).ToList();

        var allThemes = (await _unitOfWork.SiteThemeRepository.GetThemes()).ToList();
        var totalThemesToIterate = themeFiles.Count;
        var themeIteratedCount = 0;

        // First remove any files from allThemes that are User Defined and not on disk
        var userThemes = allThemes.Where(t => t.Provider == ThemeProvider.User).ToList();
        foreach (var userTheme in userThemes)
        {
            var filepath = Parser.Parser.NormalizePath(
                _directoryService.FileSystem.Path.Join(_directoryService.SiteThemeDirectory, userTheme.FileName));
            if (!_directoryService.FileSystem.File.Exists(filepath))
            {
                // I need to do the removal different. I need to update all userpreferences to use DefaultTheme
                allThemes.Remove(userTheme);
                await RemoveTheme(userTheme);

                await _messageHub.Clients.All.SendAsync(SignalREvents.SiteThemeProgress,
                    MessageFactory.SiteThemeProgressEvent(1, totalThemesToIterate, userTheme.FileName, 0F));
            }
        }

        // Add new custom themes
        var allThemeNames = allThemes.Select(t => t.NormalizedName).ToList();
        foreach (var themeFile in themeFiles)
        {
            var themeName =
                Parser.Parser.Normalize(_directoryService.FileSystem.Path.GetFileNameWithoutExtension(themeFile));
            if (allThemeNames.Contains(themeName))
            {
                themeIteratedCount += 1;
                continue;
            }
            _unitOfWork.SiteThemeRepository.Add(new SiteTheme()
            {
                Name = _directoryService.FileSystem.Path.GetFileNameWithoutExtension(themeFile),
                NormalizedName = themeName,
                FileName = _directoryService.FileSystem.Path.GetFileName(themeFile),
                Provider = ThemeProvider.User,
                IsDefault = false,
            });
            await _messageHub.Clients.All.SendAsync(SignalREvents.SiteThemeProgress,
                MessageFactory.SiteThemeProgressEvent(themeIteratedCount, totalThemesToIterate, themeName, themeIteratedCount / (totalThemesToIterate * 1.0f)));
            themeIteratedCount += 1;
        }


        if (_unitOfWork.HasChanges())
        {
            await _unitOfWork.CommitAsync();
        }

        await _messageHub.Clients.All.SendAsync(SignalREvents.SiteThemeProgress,
            MessageFactory.SiteThemeProgressEvent(totalThemesToIterate, totalThemesToIterate, "", 1F));

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
            if (theme == null) throw new KavitaException("Theme file missing or invalid");

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
