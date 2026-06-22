using System;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs.KavitaPlus;
using Kavita.Models.Entities.History;

namespace Kavita.API.Repositories;

public interface IKavitaPlusAuditRepository
{
    void Add(KavitaPlusAuditLog entry);
    Task DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct = default);

    Task<PagedList<KavitaPlusAuditEntryDto>> GetPagedAsync(
        KavitaPlusAuditFilterDto filter, UserParams userParams, CancellationToken ct = default);

    Task<PagedList<KavitaPlusAuditEntryDto>> GetMyActivityAsync(
        int userId, KavitaPlusAuditFilterDto filter, UserParams userParams, CancellationToken ct = default);

    Task<KavitaPlusAuditStatsDto> GetStatsAsync(CancellationToken ct = default);

    Task<KavitaPlusAuditSeriesInfoDto> GetSeriesInfoAsync(
        int seriesId, int callingUserId, bool isAdmin, CancellationToken ct = default);

    Task MarkAsRetriedAsync(long id, CancellationToken ct = default);
}
