using System.Threading.Tasks;
using Kavita.Models.Entities;

namespace Kavita.API.Services;

public interface IFontService
{
    public static readonly string DefaultFont = "Default";

    Task<EpubFont> CreateFontFromFileAsync(string path);
    Task Delete(int fontId);
    Task<EpubFont> CreateFontFromUrl(string url);
    Task<bool> IsFontInUse(int fontId);
}
