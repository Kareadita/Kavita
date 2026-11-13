using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.API.Repositories;
using Kavita.Models.DTOs.Kobo;
using Kavita.Models.Entities.Kobo;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;

public class KoboSyncPointRepository(DataContext context, IMapper mapper): IKoboSyncPointRepository
{
    public void Update(KoboSyncPoint syncPoint)
    {
        context.KoboSyncPoints.Update(syncPoint).State = EntityState.Modified;
    }

    public async Task<KoboSyncPoint> GetSyncPoint(int userId, int? syncPointId, string? koboStoreSyncToken, CancellationToken cancellationToken)
    {
        KoboSyncPoint? syncPoint = null;
        if (syncPointId is not null)
        {
            syncPoint = await context.KoboSyncPoints
                .Where(sp => sp.UserId == userId && sp.Id == syncPointId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (syncPoint is null)
        {
            syncPoint = new KoboSyncPoint
            {
                UserId = userId,
                ChapterSyncTime = DateTime.MinValue,
                KoboStoreSyncToken = koboStoreSyncToken,
            };

            context.KoboSyncPoints.Add(syncPoint);
        }
        else if (!string.IsNullOrEmpty(koboStoreSyncToken))
        {
            syncPoint.KoboStoreSyncToken = koboStoreSyncToken;
        }

        return syncPoint;
    }

    public Task<KoboSyncPoint?> GetSyncPoint(int userId, string koboDeviceId, CancellationToken cancellationToken)
    {
        return context.KoboSyncPoints
            .Where(sp => sp.UserId == userId && sp.KoboDeviceId == koboDeviceId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<KoboSyncPoint?> GetSyncPoint(int userId, int syncPointId, CancellationToken cancellationToken)
    {
        return context.KoboSyncPoints
            .Where(sp => sp.UserId == userId && sp.Id == syncPointId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<List<KoboSyncPointDto>> GetSyncPoints(int userId, CancellationToken cancellationToken)
    {
        return context.KoboSyncPoints
            .Where(sp => sp.UserId == userId)
            .ProjectTo<KoboSyncPointDto>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }

    public Task DeleteOthersForDeviceId(int userId, int syncPointId, string koboDeviceId, CancellationToken cancellationToken)
    {
        return context.KoboSyncPoints
            .Where(sp => sp.UserId == userId && sp.Id != syncPointId && sp.KoboDeviceId == koboDeviceId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task DeleteSyncPoint(int userId, int syncPointId, CancellationToken cancellationToken)
    {
        return context.KoboSyncPoints
            .Where(sp => sp.UserId == userId && sp.Id == syncPointId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
