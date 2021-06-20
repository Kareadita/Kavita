using System.Threading.Tasks;
using API.DTOs;

namespace API.Interfaces.Services
{
    public interface IStatsService
    {
        Task PathData(ClientInfoDto clientInfoDto);
        Task FinalizeStats();
        Task CollectRelevantData();
        Task CollectAndSendStatsData();
    }
}