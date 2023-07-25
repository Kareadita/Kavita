using System;
using System.Text.Json;
using Microsoft.Extensions.Hosting;

namespace API.Services;
#nullable enable


public interface ILocalizationService
{
    void LoadLanguage(string languageCode);
    //string Get(string key);
}

public class LocalizationService : ILocalizationService
{
    private readonly IDirectoryService _directoryService;
    private readonly string _localizationDirectory;
    private dynamic? _languageLocale;

    public LocalizationService(IDirectoryService directoryService, IHostEnvironment environment)
    {
        _directoryService = directoryService;
        if (environment.IsDevelopment())
        {
            _localizationDirectory = directoryService.FileSystem.Path.Join(
                directoryService.FileSystem.Directory.GetCurrentDirectory(),
                "..", "UI/Web/src/assets/langs");
        }
        else
        {
            _localizationDirectory = directoryService.FileSystem.Path.Join(
                directoryService.FileSystem.Directory.GetCurrentDirectory(),
                "wwwroot", "assets/langs");
        }
    }

    /// <summary>
    /// Loads a language
    /// </summary>
    /// <param name="languageCode"></param>
    /// <returns></returns>
    public void LoadLanguage(string languageCode)
    {
        var languageFile = _directoryService.FileSystem.Path.Join(_localizationDirectory, languageCode + ".json");
        if (!_directoryService.FileSystem.FileInfo.New(languageFile).Exists)
            throw new ArgumentException($"Language {languageCode} does not exist");

        var json = _directoryService.FileSystem.File.ReadAllText(languageFile);
        _languageLocale = JsonSerializer.Deserialize<dynamic>(json);
    }

    // public string Get(string key)
    // {
    //     if (_languageLocale == null) return key;
    //     return _languageLocale.
    //
    // }
}
