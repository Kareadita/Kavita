using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;

namespace API.Services;
#nullable enable


public interface ILocalizationService
{
    Task<Dictionary<string, string>> LoadLanguage(string languageCode);
    Task<string> Get(string locale, string key, params object[] args);
}

public class LocalizationService : ILocalizationService
{
    private readonly IDirectoryService _directoryService;
    private readonly IMemoryCache _cache;
    private readonly string _localizationDirectory;


    public LocalizationService(IDirectoryService directoryService, IHostEnvironment environment, IMemoryCache cache)
    {
        _directoryService = directoryService;
        _cache = cache;
        if (environment.IsDevelopment())
        {
            _localizationDirectory = directoryService.FileSystem.Path.Join(
                directoryService.FileSystem.Directory.GetCurrentDirectory(),
                "..", "UI/Web/src/assets/i18n");
        }
        else
        {
            _localizationDirectory = directoryService.FileSystem.Path.Join(
                directoryService.FileSystem.Directory.GetCurrentDirectory(),
                "wwwroot", "assets/i18n");
        }
    }

    /// <summary>
    /// Loads a language
    /// </summary>
    /// <param name="languageCode"></param>
    /// <returns></returns>
    public async Task<Dictionary<string, string>> LoadLanguage(string languageCode)
    {
        var languageFile = _directoryService.FileSystem.Path.Join(_localizationDirectory, languageCode + ".json");
        if (!_directoryService.FileSystem.FileInfo.New(languageFile).Exists)
            throw new ArgumentException($"Language {languageCode} does not exist");

        var json = await _directoryService.FileSystem.File.ReadAllTextAsync(languageFile);
        return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
    }

    public async Task<string> Get(string locale, string key, params object[] args)
    {
        // Check if the translation for the given locale is cached
        if (!_cache.TryGetValue($"{locale}_{key}", out string translatedString))
        {
            // Load the locale JSON file
            var translationData = await LoadLanguage(locale);

            // Find the translation for the given key
            if (translationData.TryGetValue(key, out string value))
            {
                translatedString = value;

                // Cache the translation for subsequent requests
                _cache.Set($"{locale}_{key}", translatedString, TimeSpan.FromMinutes(15)); // Cache for 15 minutes
            }
            else
            {
                // If the key is not found, use the key as the translated string
                translatedString = key;
            }
        }

        // Format the translated string with arguments
        if (args.Length > 0)
        {
            translatedString = string.Format(translatedString, args);
        }

        return translatedString;
    }
}
