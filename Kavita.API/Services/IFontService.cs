using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Font;
using Kavita.Models.Entities;

namespace Kavita.API.Services;

public interface IFontService
{
    Task<EpubFont> CreateFontFromFileAsync(string path, CancellationToken ct = default);
    Task<FontDeleteResultDto> DeleteFamily(int fontId, bool forceDelete, CancellationToken ct = default);
    Task<EpubFont[]> CreateFontsFromUrl(string url, CancellationToken ct = default);
}
