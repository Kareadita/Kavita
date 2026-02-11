using System.Threading.Tasks;
using Kavita.Models.DTOs.Progress;
using Kavita.Models.Entities.Progress;

namespace Kavita.API.Services.Reading;

public interface IReadingSessionService
{
    Task UpdateProgress(int userId, ProgressDto progressDto, ClientInfoData? clientInfo, int? deviceId);
}
