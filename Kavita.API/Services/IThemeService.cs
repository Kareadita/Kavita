using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Theme;
using Kavita.Models.Entities;

namespace Kavita.API.Services;

public interface IThemeService
{
    Task<string> GetContent(int themeId, CancellationToken ct = default);
    Task UpdateDefault(int themeId, CancellationToken ct = default);

    /// <summary>
    /// Browse theme repo for themes to download
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<List<DownloadableSiteThemeDto>> GetDownloadableThemes(CancellationToken ct = default);

    Task<SiteTheme> DownloadRepoTheme(DownloadableSiteThemeDto dto, CancellationToken ct = default);
    Task DeleteTheme(int siteThemeId, CancellationToken ct = default);
    Task<SiteTheme> CreateThemeFromFile(string tempFile, string username, CancellationToken ct = default);
    Task SyncThemes(CancellationToken ct = default);
}
