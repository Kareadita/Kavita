using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Dashboard;
using Kavita.Models.DTOs.SideNav;
using Kavita.Models.Entities.User;

namespace Kavita.API.Services;

/// <summary>
/// For SideNavStream and DashboardStream manipulation
/// </summary>
public interface IStreamService
{
    Task<IEnumerable<DashboardStreamDto>> GetDashboardStreams(int userId, bool visibleOnly = true, CancellationToken ct = default);
    Task<IEnumerable<SideNavStreamDto>> GetSidenavStreams(int userId, bool visibleOnly = true, CancellationToken ct = default);
    Task<IEnumerable<ExternalSourceDto>> GetExternalSources(int userId, CancellationToken ct = default);
    Task<DashboardStreamDto> CreateDashboardStreamFromSmartFilter(int userId, int smartFilterId, CancellationToken ct = default);
    Task UpdateDashboardStream(int userId, DashboardStreamDto dto, CancellationToken ct = default);
    Task UpdateDashboardStreamPosition(int userId, UpdateStreamPositionDto dto, CancellationToken ct = default);
    Task UpdateSideNavStreamBulk(int userId, BulkUpdateSideNavStreamVisibilityDto dto, CancellationToken ct = default);
    Task<SideNavStreamDto> CreateSideNavStreamFromSmartFilter(int userId, int smartFilterId, CancellationToken ct = default);
    Task<SideNavStreamDto> CreateSideNavStreamFromExternalSource(int userId, int externalSourceId, CancellationToken ct = default);
    Task UpdateSideNavStream(int userId, SideNavStreamDto dto, CancellationToken ct = default);
    Task UpdateSideNavStreamPosition(int userId, UpdateStreamPositionDto dto, CancellationToken ct = default);
    Task<ExternalSourceDto> CreateExternalSource(int userId, ExternalSourceDto dto, CancellationToken ct = default);
    Task<ExternalSourceDto> UpdateExternalSource(int userId, ExternalSourceDto dto, CancellationToken ct = default);
    Task DeleteExternalSource(int userId, int externalSourceId, CancellationToken ct = default);
    Task DeleteSideNavSmartFilterStream(int userId, int sideNavStreamId, CancellationToken ct = default);
    Task DeleteDashboardSmartFilterStream(int userId, int dashboardStreamId, CancellationToken ct = default);
    Task RenameSmartFilterStreams(AppUserSmartFilter smartFilter, CancellationToken ct = default);
}
