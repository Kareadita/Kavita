using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Font;
using Kavita.Models.Entities;

namespace Kavita.API.Repositories;

public interface IEpubFontRepository
{
    void Add(EpubFont font);
    void Remove(EpubFont font);
    void Update(EpubFont font);
    Task<IList<EpubFontDto>> GetFontDtosAsync(CancellationToken ct = default);
    Task<EpubFontDto?> GetFontDtoAsync(int fontId, CancellationToken ct = default);
    Task<EpubFontDto?> GetFontDtoByNameAsync(string name, CancellationToken ct = default);
    Task<IList<EpubFont>> GetFontsAsync(CancellationToken ct = default);
    Task<EpubFont?> GetFontAsync(int fontId, CancellationToken ct = default);
    Task<EpubFont?> GetFontByNameAsync(string name, CancellationToken ct = default);
    Task<bool> IsFontFamilyInUseAsync(string family, CancellationToken ct = default);
    Task<IList<EpubFont>> GetFontsByFamilyAsync(string family, CancellationToken ct = default);
}
