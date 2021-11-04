using System.Threading.Tasks;
using API.DTOs.Stats;

namespace API.Interfaces.Services
{
    public interface IStatsService
    {
        Task RecordClientInfo(ClientInfoDto clientInfoDto);
        Task Send();
    }
}
