using System.Threading.Tasks;
using Kavita.Models.DTOs.Koreader;

namespace Kavita.API.Services;

public interface IKoreaderService
{
    Task SaveProgress(KoreaderBookDto koreaderBookDto, int userId);
    Task<KoreaderBookDto> GetProgress(string bookHash, int userId);
}
