using System.Collections.Generic;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Theme;
using Kavita.Models.Entities;

namespace Kavita.API.Services;

public interface IThemeService
{
    Task<string> GetContent(int themeId);
    Task UpdateDefault(int themeId);
    /// <summary>
    /// Browse theme repo for themes to download
    /// </summary>
    /// <returns></returns>
    Task<List<DownloadableSiteThemeDto>> GetDownloadableThemes();

    Task<SiteTheme> DownloadRepoTheme(DownloadableSiteThemeDto dto);
    Task DeleteTheme(int siteThemeId);
    Task<SiteTheme> CreateThemeFromFile(string tempFile, string username);
    Task SyncThemes();
}
