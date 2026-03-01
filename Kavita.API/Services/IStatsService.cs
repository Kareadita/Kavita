using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Stats;

namespace Kavita.API.Services;

public interface IStatsService
{
    Task Send(CancellationToken ct = default);
    Task<ServerInfoSlimDto> GetServerInfoSlim(CancellationToken ct = default);
    Task SendCancellation(CancellationToken ct = default);
}
