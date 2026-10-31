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
    public async Task<KoboSyncPoint> GetSyncPoint(int userId, int? syncPointId, CancellationToken cancellationToken)
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
            };

            context.KoboSyncPoints.Add(syncPoint);
        }


        return syncPoint;
    }
}
