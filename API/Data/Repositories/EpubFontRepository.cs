#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Font;
using API.Entities;
using API.Extensions;
using API.Services.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IEpubFontRepository
{
    void Add(EpubFont font);
    void Remove(EpubFont font);
    void Update(EpubFont font);
    Task<IEnumerable<EpubFontDto>> GetFontDtosAsync();
    Task<EpubFontDto?> GetFontDtoAsync(int fontId);
    Task<EpubFontDto?> GetFontDtoByNameAsync(string name);
    Task<IEnumerable<EpubFont>> GetFontsAsync();
    Task<EpubFont?> GetFontAsync(int fontId);
    Task<bool> IsFontInUseAsync(int fontId);
}

public class EpubFontRepository: IEpubFontRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public EpubFontRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public void Add(EpubFont font)
    {
        _context.Add(font);
    }

    public void Remove(EpubFont font)
    {
        _context.Remove(font);
    }

    public void Update(EpubFont font)
    {
        _context.Entry(font).State = EntityState.Modified;
    }

    public async Task<IEnumerable<EpubFontDto>> GetFontDtosAsync()
    {
        return await _context.EpubFont
            .OrderBy(s => s.Name == FontService.DefaultFont ? -1 : 0)
            .ThenBy(s => s)
            .ProjectTo<EpubFontDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<EpubFontDto?> GetFontDtoAsync(int fontId)
    {
        return await _context.EpubFont
            .Where(f => f.Id == fontId)
            .ProjectTo<EpubFontDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    public async Task<EpubFontDto?> GetFontDtoByNameAsync(string name)
    {
        return await _context.EpubFont
            .Where(f => f.NormalizedName.Equals(name.ToNormalized()))
            .ProjectTo<EpubFontDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<EpubFont>> GetFontsAsync()
    {
        return await _context.EpubFont
            .ToListAsync();
    }

    public async Task<EpubFont?> GetFontAsync(int fontId)
    {
        return await _context.EpubFont
            .Where(f => f.Id == fontId)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> IsFontInUseAsync(int fontId)
    {
        return await _context.AppUserReadingProfiles
            .Join(_context.EpubFont,
                preference => preference.BookReaderFontFamily,
                font => font.Name,
                (preference, font) => new { preference, font })
            .AnyAsync(joined => joined.font.Id == fontId);
    }

}
