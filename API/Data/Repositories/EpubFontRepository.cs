#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Font;
using API.Entities;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IEpubFontRepository
{
    void Add(EpubFont font);
    void Remove(EpubFont font);
    void Update(EpubFont font);
    Task<IEnumerable<EpubFontDto>> GetFontDtos();
    Task<EpubFontDto?> GetFontDto(int fontId);
    Task<IEnumerable<EpubFont>> GetFonts();
    Task<EpubFont?> GetFont(int fontId);
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

    public async Task<IEnumerable<EpubFontDto>> GetFontDtos()
    {
        return await _context.EpubFont
            .ProjectTo<EpubFontDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<EpubFontDto?> GetFontDto(int fontId)
    {
        return await _context.EpubFont
            .Where(f => f.Id == fontId)
            .ProjectTo<EpubFontDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<EpubFont>> GetFonts()
    {
        return await _context.EpubFont
            .ToListAsync();
    }

    public async Task<EpubFont?> GetFont(int fontId)
    {
        return await _context.EpubFont
            .Where(f => f.Id == fontId)
            .FirstOrDefaultAsync();
    }
}
