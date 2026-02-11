using System.Collections.Generic;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Update;

namespace Kavita.API.Services;

public interface IVersionUpdaterService
{
    Task<UpdateNotificationDto?> CheckForUpdate();
    Task PushUpdate(UpdateNotificationDto update);
    Task<IList<UpdateNotificationDto>> GetAllReleases(int count = 0);
    Task<int> GetNumberOfReleasesBehind(bool stableOnly = false);
    void BustGithubCache();
}
