using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Repositories;
using Kavita.Models.Entities.Kobo;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;

public class KoboSyncPointRepository(DataContext context): IKoboSyncPointRepository
{
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

    public Task DeleteOthersForDeviceId(int userId, int syncPointId, string koboDeviceId, CancellationToken cancellationToken)
    {
        return context.KoboSyncPoints
            .Where(sp => sp.UserId == userId && sp.Id != syncPointId && sp.KoboDeviceId == koboDeviceId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
