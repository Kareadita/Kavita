using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.Common.Extensions;
using Kavita.Models;
using Kavita.Models.DTOs.Font;
using Kavita.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;

public class EpubFontRepository(DataContext context, IMapper mapper) : IEpubFontRepository
{
    public void Add(EpubFont font)
    {
        context.Add(font);
    }

    public void Remove(EpubFont font)
    {
        context.Remove(font);
    }

    public void Update(EpubFont font)
    {
        context.Entry(font).State = EntityState.Modified;
    }

    public async Task<IList<EpubFontDto>> GetFontDtosAsync(CancellationToken ct = default)
    {
        return await context.EpubFont
            .OrderBy(s => s.Name == Defaults.DefaultFont ? -1 : 0)
            .ThenBy(s => s)
            .ProjectTo<EpubFontDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    public async Task<EpubFontDto?> GetFontDtoAsync(int fontId, CancellationToken ct = default)
    {
        return await context.EpubFont
            .Where(f => f.Id == fontId)
            .ProjectTo<EpubFontDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<EpubFontDto?> GetFontDtoByNameAsync(string name, CancellationToken ct = default)
    {
        return await context.EpubFont
            .Where(f => f.NormalizedName.Equals(name.ToNormalized()))
            .ProjectTo<EpubFontDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IList<EpubFont>> GetFontsAsync(CancellationToken ct = default)
    {
        return await context.EpubFont.ToListAsync(ct);
    }

    public async Task<EpubFont?> GetFontAsync(int fontId, CancellationToken ct = default)
    {
        return await context.EpubFont
            .Where(f => f.Id == fontId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> IsFontInUseAsync(int fontId, CancellationToken ct = default)
    {
        return await context.AppUserReadingProfiles
            .Join(context.EpubFont,
                preference => preference.BookReaderFontFamily,
                font => font.Name,
                (preference, font) => new { preference, font })
            .AnyAsync(joined => joined.font.Id == fontId, ct);
    }

}
