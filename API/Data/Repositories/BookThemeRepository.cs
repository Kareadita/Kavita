using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Theme;
using API.Entities;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IBookThemeRepository
{
    void Add(BookTheme theme);
    void Remove(BookTheme theme);
    void Update(BookTheme theme);
    Task<IEnumerable<BookThemeDto>> GetThemeDtos();
    Task<BookThemeDto> GetThemeDto(int themeId);
    Task<BookThemeDto> GetThemeDtoByName(string themeName);
    Task<BookTheme> GetDefaultTheme();
    Task<IEnumerable<BookTheme>> GetThemes();
    Task<BookTheme> GetThemeById(int themeId);
    Task<IEnumerable<BookThemeDto>> GetThemeDtosForUser(int userId);
}

public class BookThemeRepository : IBookThemeRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public BookThemeRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }


    public async Task<IEnumerable<BookThemeDto>> GetThemeDtosForUser(int userId)
    {
        return await _context.BookTheme
            .OrderBy(t => t.SortOrder)
            .ProjectTo<BookThemeDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public void Add(BookTheme theme)
    {
        _context.Add(theme);
    }

    public void Remove(BookTheme theme)
    {
        _context.Remove(theme);
    }

    public void Update(BookTheme theme)
    {
        _context.Entry(theme).State = EntityState.Modified;
    }

    public async Task<IEnumerable<BookThemeDto>> GetThemeDtos()
    {
        return await _context.BookTheme
            .ProjectTo<BookThemeDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<BookThemeDto> GetThemeDto(int themeId)
    {
        return await _context.BookTheme
            .Where(t => t.Id == themeId)
            .ProjectTo<BookThemeDto>(_mapper.ConfigurationProvider)
            .SingleOrDefaultAsync();
    }

    public async Task<BookThemeDto> GetThemeDtoByName(string themeName)
    {
        return await _context.BookTheme
            .Where(t => t.Name.Equals(themeName))
            .ProjectTo<BookThemeDto>(_mapper.ConfigurationProvider)
            .SingleOrDefaultAsync();
    }

    /// <summary>
    /// Returns default theme, if the default theme is not available, returns the dark theme
    /// </summary>
    /// <returns></returns>
    public async Task<BookTheme> GetDefaultTheme()
    {
        var result =  await _context.BookTheme
            .Where(t => t.IsDefault)
            .SingleOrDefaultAsync();

        if (result == null)
        {
            return await _context.BookTheme
                .Where(t => t.NormalizedName == "dark")
                .SingleOrDefaultAsync();
        }

        return result;
    }

    public async Task<IEnumerable<BookTheme>> GetThemes()
    {
        return await _context.BookTheme
            .ToListAsync();
    }

    public async Task<BookTheme> GetThemeById(int themeId)
    {
        return await _context.BookTheme
            .Where(t => t.Id == themeId)
            .SingleOrDefaultAsync();
    }
}
