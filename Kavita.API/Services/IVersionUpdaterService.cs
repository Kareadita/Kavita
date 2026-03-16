using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Update;

namespace Kavita.API.Services;

public interface IVersionUpdaterService
{
    Task<UpdateNotificationDto?> CheckForUpdate(CancellationToken ct = default);
    Task PushUpdate(UpdateNotificationDto update, CancellationToken ct = default);
    Task<IList<UpdateNotificationDto>> GetAllReleases(int count = 0, CancellationToken ct = default);
    Task<int> GetNumberOfReleasesBehind(bool stableOnly = false, CancellationToken ct = default);
    void BustGithubCache(CancellationToken ct = default);
}
