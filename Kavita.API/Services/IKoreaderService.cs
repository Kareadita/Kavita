using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Koreader;

namespace Kavita.API.Services;

public interface IKoreaderService
{
    Task SaveProgress(KoreaderBookDto koreaderBookDto, int userId, CancellationToken ct = default);
    Task<KoreaderBookDto> GetProgress(string bookHash, int userId, CancellationToken ct = default);
}
