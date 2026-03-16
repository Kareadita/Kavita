using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs.Dashboard;
using Kavita.Models.Entities.User;

namespace Kavita.API.Repositories;

public interface IAppUserSmartFilterRepository
{
    void Update(AppUserSmartFilter filter);
    void Attach(AppUserSmartFilter filter);
    void Delete(AppUserSmartFilter filter);
    Task<IList<SmartFilterDto>> GetAllDtosByUserId(int userId, CancellationToken ct = default);
    Task<PagedList<SmartFilterDto>> GetPagedDtosByUserIdAsync(int userId, UserParams userParams, CancellationToken ct = default);
    Task<AppUserSmartFilter?> GetById(int smartFilterId, CancellationToken ct = default);
}
