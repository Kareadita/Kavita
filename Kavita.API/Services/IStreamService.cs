using System.Collections.Generic;
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
    Task<IEnumerable<DashboardStreamDto>> GetDashboardStreams(int userId, bool visibleOnly = true);
    Task<IEnumerable<SideNavStreamDto>> GetSidenavStreams(int userId, bool visibleOnly = true);
    Task<IEnumerable<ExternalSourceDto>> GetExternalSources(int userId);
    Task<DashboardStreamDto> CreateDashboardStreamFromSmartFilter(int userId, int smartFilterId);
    Task UpdateDashboardStream(int userId, DashboardStreamDto dto);
    Task UpdateDashboardStreamPosition(int userId, UpdateStreamPositionDto dto);
    Task UpdateSideNavStreamBulk(int userId, BulkUpdateSideNavStreamVisibilityDto dto);
    Task<SideNavStreamDto> CreateSideNavStreamFromSmartFilter(int userId, int smartFilterId);
    Task<SideNavStreamDto> CreateSideNavStreamFromExternalSource(int userId, int externalSourceId);
    Task UpdateSideNavStream(int userId, SideNavStreamDto dto);
    Task UpdateSideNavStreamPosition(int userId, UpdateStreamPositionDto dto);
    Task<ExternalSourceDto> CreateExternalSource(int userId, ExternalSourceDto dto);
    Task<ExternalSourceDto> UpdateExternalSource(int userId, ExternalSourceDto dto);
    Task DeleteExternalSource(int userId, int externalSourceId);
    Task DeleteSideNavSmartFilterStream(int userId, int sideNavStreamId);
    Task DeleteDashboardSmartFilterStream(int userId, int dashboardStreamId);
    Task RenameSmartFilterStreams(AppUserSmartFilter smartFilter);
}
