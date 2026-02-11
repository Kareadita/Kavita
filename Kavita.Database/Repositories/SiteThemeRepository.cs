using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.API.Repositories;
using Kavita.Models.DTOs.Theme;
using Kavita.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;

public class SiteThemeRepository(DataContext context, IMapper mapper) : ISiteThemeRepository
{
    public void Add(SiteTheme theme)
    {
        context.Add(theme);
    }

    public void Remove(SiteTheme theme)
    {
        context.Remove(theme);
    }

    public void Update(SiteTheme siteTheme)
    {
        context.Entry(siteTheme).State = EntityState.Modified;
    }

    public async Task<IEnumerable<SiteThemeDto>> GetThemeDtos()
    {
        return await context.SiteTheme
            .ProjectTo<SiteThemeDto>(mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<SiteThemeDto?> GetThemeDtoByName(string themeName)
    {
        return await context.SiteTheme
            .Where(t => t.Name.Equals(themeName))
            .ProjectTo<SiteThemeDto>(mapper.ConfigurationProvider)
            .SingleOrDefaultAsync();
    }

    /// <summary>
    /// Returns default theme, if the default theme is not available, returns the dark theme
    /// </summary>
    /// <returns></returns>
    public async Task<SiteTheme> GetDefaultTheme()
    {
        var result =  await context.SiteTheme
            .Where(t => t.IsDefault)
            .FirstOrDefaultAsync();

        if (result == null)
        {
            return await context.SiteTheme
                .Where(t => t.NormalizedName == SiteTheme.DefaultTheme.NormalizedName)
                .SingleAsync();
        }

        return result;
    }

    public async Task<IEnumerable<SiteTheme>> GetThemes()
    {
        return await context.SiteTheme
            .ToListAsync();
    }

    public async Task<SiteTheme> GetTheme(int themeId)
    {
        return await context.SiteTheme
            .Where(t => t.Id == themeId)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> IsThemeInUse(int themeId)
    {
        return await context.AppUserPreferences
            .AnyAsync(p => p.Theme.Id == themeId);
    }

    public async Task<SiteThemeDto?> GetThemeDto(int themeId)
    {
        return await context.SiteTheme
            .Where(t => t.Id == themeId)
            .ProjectTo<SiteThemeDto>(mapper.ConfigurationProvider)
            .SingleOrDefaultAsync();
    }
}
