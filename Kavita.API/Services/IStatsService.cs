using System.Threading.Tasks;
using Kavita.Models.DTOs.Stats;

namespace Kavita.API.Services;

public interface IStatsService
{
    Task Send();
    Task<ServerInfoSlimDto> GetServerInfoSlim();
    Task SendCancellation();
}
