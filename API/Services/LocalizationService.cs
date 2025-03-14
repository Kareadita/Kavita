using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using API.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;

namespace API.Services;
#nullable enable

public class KavitaLocale
{
    public string FileName { get; set; } // Key
    public string RenderName { get; set; }
    public float TranslationCompletion { get; set; }
    public bool IsRtL { get; set; }
    public string Hash { get; set; } // ETAG hash so I can run my own localization busting implementation
}


public interface ILocalizationService
{
    Task<string> Get(string locale, string key, params object[] args);
    Task<string> Translate(int userId, string key, params object[] args);
    IEnumerable<KavitaLocale> GetLocales();
}

public class LocalizationService : ILocalizationService
{
    private readonly IDirectoryService _directoryService;
    private readonly IMemoryCache _cache;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// The locales for the UI
    /// </summary>
    private readonly string _localizationDirectoryUi;

    private readonly MemoryCacheEntryOptions _cacheOptions;


    public LocalizationService(IDirectoryService directoryService,
        IHostEnvironment environment, IMemoryCache cache, IUnitOfWork unitOfWork)
    {
        _directoryService = directoryService;
        _cache = cache;
        _unitOfWork = unitOfWork;
        if (environment.IsDevelopment())
        {
            _localizationDirectoryUi = directoryService.FileSystem.Path.Join(
                directoryService.FileSystem.Directory.GetCurrentDirectory(),
                "../UI/Web/src/assets/langs");
        } else if (environment.EnvironmentName.Equals("Testing", StringComparison.OrdinalIgnoreCase))
        {
            _localizationDirectoryUi = directoryService.FileSystem.Path.Join(
                directoryService.FileSystem.Directory.GetCurrentDirectory(),
                "/../../../../../UI/Web/src/assets/langs");
        }
        else
        {
            _localizationDirectoryUi = directoryService.FileSystem.Path.Join(
                directoryService.FileSystem.Directory.GetCurrentDirectory(),
                "wwwroot", "assets/langs");
        }

        _cacheOptions = new MemoryCacheEntryOptions()
            .SetSize(1)
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(15));
    }

    /// <summary>
    /// Loads a language, if language is blank, falls back to english
    /// </summary>
    /// <param name="languageCode"></param>
    /// <returns></returns>
    public async Task<Dictionary<string, string>?> LoadLanguage(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode)) languageCode = "en";
        var languageFile = _directoryService.FileSystem.Path.Join(_directoryService.LocalizationDirectory, languageCode + ".json");
        if (!_directoryService.FileSystem.FileInfo.New(languageFile).Exists)
            throw new ArgumentException($"Language {languageCode} does not exist");

        var json = await _directoryService.FileSystem.File.ReadAllTextAsync(languageFile);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
    }

    public async Task<string> Get(string locale, string key, params object[] args)
    {

        // Check if the translation for the given locale is cached
        var cacheKey = $"{locale}_{key}";
        if (!_cache.TryGetValue(cacheKey, out string? translatedString))
        {
            // Load the locale JSON file
            var translationData = await LoadLanguage(locale);

            // Find the translation for the given key
            if (translationData != null && translationData.TryGetValue(key, out var value))
            {
                translatedString = value;

                // Cache the translation for subsequent requests
                _cache.Set(cacheKey, translatedString, _cacheOptions);
            }
        }


        if (string.IsNullOrEmpty(translatedString))
        {
            if (!locale.Equals("en"))
            {
                return await Get("en", key, args);
            }
            return key;
        }

        // Format the translated string with arguments
        if (args.Length > 0)
        {
            translatedString = string.Format(translatedString, args);
        }

        return translatedString;
    }

    /// <summary>
    /// Returns a translated string for a given user's locale, falling back to english or the key if missing
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="key"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public async Task<string> Translate(int userId, string key, params object[] args)
    {
        var userLocale = await _unitOfWork.UserRepository.GetLocale(userId);
        return await Get(userLocale, key, args);
    }


    /// <summary>
    /// Returns all available locales that exist on both the Frontend and the Backend
    /// </summary>
    /// <returns></returns>
    public IEnumerable<KavitaLocale> GetLocales()
    {
        var uiLanguages = _directoryService
        .GetFilesWithExtension(_directoryService.FileSystem.Path.GetFullPath(_localizationDirectoryUi), @"\.json");
        var backendLanguages = _directoryService
            .GetFilesWithExtension(_directoryService.LocalizationDirectory, @"\.json");

        var locales = new Dictionary<string, KavitaLocale>();
        var localeCounts = new Dictionary<string, Tuple<int, int>>();  // fileName -> (nonEmptyValues, totalKeys)

        // First pass: collect all files and count non-empty strings

        // Process UI language files
        foreach (var file in uiLanguages)
        {
            var fileName = _directoryService.FileSystem.Path.GetFileNameWithoutExtension(file);
            var fileContent = _directoryService.FileSystem.File.ReadAllText(file);
            var hash = ComputeHash(fileContent);

            var counts = CalculateNonEmptyStrings(fileContent);

            if (localeCounts.TryGetValue(fileName, out var existingCount))
            {
                // Update existing counts
                localeCounts[fileName] = Tuple.Create(
                    existingCount.Item1 + counts.Item1,
                    existingCount.Item2 + counts.Item2
                );
            }
            else
            {
                // Add new counts
                localeCounts[fileName] = counts;
            }

            if (!locales.TryGetValue(fileName, out var locale))
            {
                locales[fileName] = new KavitaLocale
                {
                    FileName = fileName,
                    RenderName = GetDisplayName(fileName),
                    TranslationCompletion = 0, // Will be calculated later
                    IsRtL = IsRightToLeft(fileName),
                    Hash = hash
                };
            }
            else
            {
                // Update existing locale hash
                locale.Hash = CombineHashes(locale.Hash, hash);
            }
        }

        // Process backend language files
        foreach (var file in backendLanguages)
        {
            var fileName = _directoryService.FileSystem.Path.GetFileNameWithoutExtension(file);
            var fileContent = _directoryService.FileSystem.File.ReadAllText(file);
            var hash = ComputeHash(fileContent);

            var counts = CalculateNonEmptyStrings(fileContent);

            if (localeCounts.TryGetValue(fileName, out var existingCount))
            {
                // Update existing counts
                localeCounts[fileName] = Tuple.Create(
                    existingCount.Item1 + counts.Item1,
                    existingCount.Item2 + counts.Item2
                );
            }
            else
            {
                // Add new counts
                localeCounts[fileName] = counts;
            }

            if (!locales.TryGetValue(fileName, out var locale))
            {
                locales[fileName] = new KavitaLocale
                {
                    FileName = fileName,
                    RenderName = GetDisplayName(fileName),
                    TranslationCompletion = 0, // Will be calculated later
                    IsRtL = IsRightToLeft(fileName),
                    Hash = hash
                };
            }
            else
            {
                // Update existing locale hash
                locale.Hash = CombineHashes(locale.Hash, hash);
            }
        }

        // Second pass: calculate completion percentages based on English total
        if (localeCounts.TryGetValue("en", out var englishCounts) && englishCounts.Item2 > 0)
        {
            var englishTotalKeys = englishCounts.Item2;

            foreach (var locale in locales.Values)
            {
                if (localeCounts.TryGetValue(locale.FileName, out var counts))
                {
                    // Calculate percentage based on English total keys
                    locale.TranslationCompletion = (float)counts.Item1 / englishTotalKeys * 100;
                }
            }
        }

        return locales.Values;
    }

    // Helper methods that would need to be implemented
    private static string ComputeHash(string content)
    {
        // Implement a hashing algorithm (e.g., SHA256, MD5) to generate a hash for the content
        using var md5 = System.Security.Cryptography.MD5.Create();
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hashBytes = md5.ComputeHash(inputBytes);
        return Convert.ToBase64String(hashBytes);
    }

    private static string CombineHashes(string hash1, string hash2)
    {
        // Combine two hashes, possibly by concatenating and rehashing
        return ComputeHash(hash1 + hash2);
    }

    private static string GetDisplayName(string fileName)
    {
        // Map the filename to a human-readable display name
        // This could use a lookup table or follow a naming convention
        try
        {
            var cultureInfo = new System.Globalization.CultureInfo(fileName);
            return cultureInfo.NativeName;
        }
        catch
        {
            // Fall back to the file name if the culture isn't recognized
            return fileName;
        }
    }

    private static bool IsRightToLeft(string fileName)
    {
        // Determine if the language is right-to-left
        try
        {
            var cultureInfo = new System.Globalization.CultureInfo(fileName);
            return cultureInfo.TextInfo.IsRightToLeft;
        }
        catch
        {
            return false; // Default to left-to-right
        }
    }

    private static float CalculateTranslationCompletion(string fileContent)
    {
        try
        {
            var jsonObject = System.Text.Json.JsonDocument.Parse(fileContent);

            int totalKeys = 0;
            int nonEmptyValues = 0;

            // Count all keys and non-empty values
            CountNonEmptyValues(jsonObject.RootElement, ref totalKeys, ref nonEmptyValues);

            return totalKeys > 0 ? (nonEmptyValues * 1f) / totalKeys * 100 : 0;
        }
        catch (Exception ex)
        {
            // Consider logging the exception
            return 0; // Return 0% completion if there's an error parsing
        }
    }
    private static Tuple<int, int> CalculateNonEmptyStrings(string fileContent)
    {
        try
        {
            var jsonObject = JsonDocument.Parse(fileContent);

            var totalKeys = 0;
            var nonEmptyValues = 0;

            // Count all keys and non-empty values
            CountNonEmptyValues(jsonObject.RootElement, ref totalKeys, ref nonEmptyValues);

            return Tuple.Create(nonEmptyValues, totalKeys);
        }
        catch (Exception)
        {
            // Consider logging the exception
            return Tuple.Create(0, 0); // Return 0% completion if there's an error parsing
        }
    }

    private static void CountNonEmptyValues(JsonElement element, ref int totalKeys, ref int nonEmptyValues)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    totalKeys++;
                    var value = property.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        nonEmptyValues++;
                    }
                }
                else
                {
                    // Recursively process nested objects
                    CountNonEmptyValues(property.Value, ref totalKeys, ref nonEmptyValues);
                }
            }
        }
        else if (element.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                CountNonEmptyValues(item, ref totalKeys, ref nonEmptyValues);
            }
        }
    }

    private void CountEntries(System.Text.Json.JsonElement element, ref int total, ref int translated)
    {
        if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                CountEntries(property.Value, ref total, ref translated);
            }
        }
        else if (element.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                CountEntries(item, ref total, ref translated);
            }
        }
        else if (element.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            total++;
            string value = element.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                translated++;
            }
        }
    }
}
