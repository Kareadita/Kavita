using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Models.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Kobo;

/// <summary>
/// On device-file format change (KEPUB re-offer), rematch or clear Location per user+chapter.
/// </summary>
public class KoboLocationRematchService(
    IUnitOfWork unitOfWork,
    IKoboLocationMapper koboLocationMapper,
    ILogger<KoboLocationRematchService> logger)
    : IKoboLocationRematchService
{
    public async Task RematchAfterDeviceFileChangeAsync(int chapterId, string newDeviceOpenablePath,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(newDeviceOpenablePath)) return;

        var locations = await unitOfWork.DataContext.AppUserKoboReadingLocation
            .Where(l => l.ChapterId == chapterId)
            .ToListAsync(ct);
        var progresses = await unitOfWork.DataContext.AppUserProgresses
            .Where(p => p.ChapterId == chapterId)
            .ToListAsync(ct);

        if (locations.Count == 0 && progresses.All(p => string.IsNullOrEmpty(p.BookScrollId)))
        {
            return;
        }

        var progressByUser = progresses.ToDictionary(p => p.AppUserId);
        var locationByUser = locations.ToDictionary(l => l.AppUserId);
        var userIds = progressByUser.Keys.Union(locationByUser.Keys).ToHashSet();

        var cleared = 0;
        var remapped = 0;
        var kept = 0;

        foreach (var userId in userIds)
        {
            progressByUser.TryGetValue(userId, out var progress);
            locationByUser.TryGetValue(userId, out var location);

            var bookScrollId = progress?.BookScrollId;
            var pagesRead = progress?.PagesRead ?? 0;

            KoboMappedLocation? mapped = null;
            if (!string.IsNullOrWhiteSpace(bookScrollId))
            {
                try
                {
                    mapped = await koboLocationMapper.TryMapBookScrollIdToLocationAsync(
                        newDeviceOpenablePath, pagesRead, bookScrollId, ct);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex,
                        "KEPUB rematch map failed for user {UserId} chapter {ChapterId}",
                        userId, chapterId);
                }
            }

            if (mapped != null)
            {
                if (location == null)
                {
                    unitOfWork.DataContext.AppUserKoboReadingLocation.Add(new AppUserKoboReadingLocation
                    {
                        AppUserId = userId,
                        ChapterId = chapterId,
                        LocationValue = mapped.Value,
                        LocationType = mapped.Type,
                        LocationSource = mapped.Source,
                    });
                }
                else
                {
                    location.LocationValue = mapped.Value;
                    location.LocationType = mapped.Type;
                    location.LocationSource = mapped.Source;
                }

                remapped++;
                continue;
            }

            if (location == null) continue;

            // BookScrollId remap failed — keep prior Location only if still valid in the new file.
            var stillValid = false;
            if (!string.IsNullOrEmpty(location.LocationValue))
            {
                try
                {
                    var scroll = await koboLocationMapper.TryMapLocationToBookScrollIdAsync(
                        newDeviceOpenablePath, location.LocationValue, location.LocationType,
                        location.LocationSource, ct);
                    stillValid = !string.IsNullOrEmpty(scroll);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex,
                        "KEPUB rematch validate failed for user {UserId} chapter {ChapterId}",
                        userId, chapterId);
                }
            }

            if (stillValid)
            {
                kept++;
                continue;
            }

            unitOfWork.DataContext.AppUserKoboReadingLocation.Remove(location);
            cleared++;
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync(ct);
        }

        if (remapped > 0 || cleared > 0 || kept > 0)
        {
            logger.LogInformation(
                "KEPUB Location rematch for chapter {ChapterId}: remapped={Remapped}, kept={Kept}, cleared={Cleared}",
                chapterId, remapped, kept, cleared);
        }
    }
}
