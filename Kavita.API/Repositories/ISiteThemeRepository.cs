using System.Collections.Generic;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Theme;
using Kavita.Models.Entities;

namespace Kavita.API.Repositories;

public interface ISiteThemeRepository
{
    void Add(SiteTheme theme);
    void Remove(SiteTheme theme);
    void Update(SiteTheme siteTheme);
    Task<IEnumerable<SiteThemeDto>> GetThemeDtos();
    Task<SiteThemeDto?> GetThemeDto(int themeId);
    Task<SiteThemeDto?> GetThemeDtoByName(string themeName);
    Task<SiteTheme> GetDefaultTheme();
    Task<IEnumerable<SiteTheme>> GetThemes();
    Task<SiteTheme?> GetTheme(int themeId);
    Task<bool> IsThemeInUse(int themeId);
}
