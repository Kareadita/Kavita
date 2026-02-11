using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.SideNav;
using Kavita.Models.Entities.User;

namespace Kavita.API.Repositories;

public interface IAppUserExternalSourceRepository
{
    void Update(AppUserExternalSource source);
    void Delete(AppUserExternalSource source);
    Task<AppUserExternalSource?> GetById(int externalSourceId, CancellationToken ct = default);
    Task<IList<ExternalSourceDto>> GetExternalSources(int userId, CancellationToken ct = default);
    Task<bool> ExternalSourceExists(int userId, string name, string host, string apiKey, CancellationToken ct = default);
}
