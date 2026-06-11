using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services.Plus;
using Kavita.Models.DTOs.KavitaPlus;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.Audit;
using Kavita.Models.Entities.Enums.KavitaPlus;
using Kavita.Models.Entities.Enums.UserPreferences;
using Kavita.Models.Entities.Scrobble;
using Kavita.Models.Entities.User;
using Kavita.Models.Extensions;
using Kavita.Services.Scanner;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Plus.ScrobbleService;

public interface ISeriesScrobbleService : IScrobbleProviderService;

/// <summary>
/// A <see cref="IScrobbleProviderService"/> implementation for <see cref="ScrobbleProvider"/>'s that track data
/// based on series. (Mangabaka, AniList, etc.)
/// </summary>
public abstract class SeriesScrobbleService<T>(ILogger<T> logger, IUnitOfWork unitOfWork, IKavitaPlusAuditService auditService): ISeriesScrobbleService
where T: IScrobbleProviderService
{
    protected abstract ScrobbleProvider Provider { get; }

    protected abstract IReadOnlyList<ScrobbleEventType> SupportedEvents { get; }

    protected abstract void SetScrobbleIds(ScrobbleEvent evt, Series series);

    public abstract RateProfile RateProfile { get; }

    public abstract bool IsTokenValid(string token);

    public async Task ScrobbleReadStatusUpdates(ScrobbleUpdateContext ctx, ScrobbleReadStatus status,
        TransitionRuleKind? ruleKind = null, string? ruleHash = null, CancellationToken ct = default)
    {
        if (!SupportedEvents.Contains(ScrobbleEventType.ReadStatusUpdate) || ctx.Chapter != null) return;

        var existingEvent = await unitOfWork.ScrobbleRepository.GetEvent(
            Provider, ctx.User.Id, ctx.Series.Id, null, ScrobbleEventType.ReadStatusUpdate, true, ct
        );

        if (existingEvent is { IsProcessed: false })
        {
            // NOTE: I'm seeing this when statuses align, maybe we should gate the log to avoid extra noise
            logger.LogDebug("Overriding scrobble event for {Series} from Read Status {Status} -> {UpdatedStatus}",
                ctx.Series.Name, existingEvent.ReadStatus, status);

            existingEvent.ReadStatus = status;
            existingEvent.IsBackFill &= ctx.IsBackfill;
            existingEvent.TransitionRuleKind = ruleKind;
            existingEvent.RuleHashSnapshot = ruleHash;

            unitOfWork.ScrobbleRepository.Update(existingEvent);
            await unitOfWork.CommitAsync(ct);

            await auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventUpdated, ctx.Series.Id,
                new AuditLogScrobbleParamsDto
                {
                    Provider = Provider,
                    ScrobbleEventType = ScrobbleEventType.ReadStatusUpdate,
                    ReadStatus = status,
                    TransitionRuleKind = ruleKind,
                }, AuditStatus.Info, userId: ctx.User.Id, ct: ct);
            return;
        }

        var scrobbleEvent = new ScrobbleEvent
        {
            ScrobbleEventType = ScrobbleEventType.ReadStatusUpdate,
            ScrobbleProvider = Provider,
            Format = ctx.Series.Library.Type.ConvertToPlusMediaFormat(ctx.Series.Format),
            SeriesId = ctx.Series.Id,
            LibraryId = ctx.Series.LibraryId,
            AppUserId = ctx.User.Id,
            ReadStatus = status,
            IsBackFill = ctx.IsBackfill,
            TransitionRuleKind = ruleKind,
            RuleHashSnapshot = ruleHash,
        };

        SetScrobbleIds(scrobbleEvent, ctx.Series);

        unitOfWork.ScrobbleRepository.Attach(scrobbleEvent);
        await unitOfWork.CommitAsync(ct);

        await auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventCreated, ctx.Series.Id,
            new AuditLogScrobbleParamsDto
            {
                Provider = Provider,
                ScrobbleEventType = ScrobbleEventType.ReadStatusUpdate,
                ReadStatus = status,
                TransitionRuleKind = ruleKind,
            }, AuditStatus.Info, userId: ctx.User.Id, ct: ct);

        logger.LogDebug("Created new scrobble event for {Series} with Read Status {Status}", ctx.Series.Name, status);
    }

    public async Task ScrobbleRatingUpdate(ScrobbleUpdateContext ctx, float rating, CancellationToken ct = default)
    {
        if (!SupportedEvents.Contains(ScrobbleEventType.ScoreUpdated) || ctx.Chapter != null) return;

        var existingEvent = await unitOfWork.ScrobbleRepository.GetEvent(
            Provider, ctx.User.Id, ctx.Series.Id, null, ScrobbleEventType.ScoreUpdated, true, ct
        );

        if (existingEvent is { IsProcessed: false })
        {
            logger.LogDebug("Overriding scrobble event for {Series} from Rating {Rating} -> {UpdatedRating}",
                ctx.Series.Name, existingEvent.Rating, rating);

            existingEvent.Rating = rating;
            existingEvent.IsBackFill &= ctx.IsBackfill;

            unitOfWork.ScrobbleRepository.Update(existingEvent);
            await unitOfWork.CommitAsync(ct);

            await auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventUpdated, ctx.Series.Id,
                new AuditLogScrobbleParamsDto
                {
                    Provider = Provider,
                    ScrobbleEventType = ScrobbleEventType.ScoreUpdated,
                    Rating = rating,
                    LibraryType = ctx.Series.Library.Type,
                }, AuditStatus.Info, userId: ctx.User.Id, ct: ct);
            return;
        }

        var scrobbleEvent = new ScrobbleEvent
        {
            ScrobbleEventType = ScrobbleEventType.ScoreUpdated,
            ScrobbleProvider = Provider,
            Format = ctx.Series.Library.Type.ConvertToPlusMediaFormat(ctx.Series.Format),
            SeriesId = ctx.Series.Id,
            LibraryId = ctx.Series.LibraryId,
            AppUserId = ctx.User.Id,
            Rating = rating,
            IsBackFill = ctx.IsBackfill,
        };

        SetScrobbleIds(scrobbleEvent, ctx.Series);

        unitOfWork.ScrobbleRepository.Attach(scrobbleEvent);
        await unitOfWork.CommitAsync(ct);

        await auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventCreated, ctx.Series.Id,
            new AuditLogScrobbleParamsDto
            {
                Provider = Provider,
                ScrobbleEventType = ScrobbleEventType.ScoreUpdated,
                Rating = rating,
                LibraryType = ctx.Series.Library.Type,
            }, AuditStatus.Info, userId: ctx.User.Id, ct: ct);

        logger.LogDebug("Created new scrobble event for {Series} with Rating {Rating}", ctx.Series.Name, rating);
    }

    public async Task ScrobbleReviewUpdate(ScrobbleUpdateContext ctx, string? reviewTitle, string reviewBody,
        CancellationToken ct = default)
    {
        if (!SupportedEvents.Contains(ScrobbleEventType.Review) || ctx.Chapter != null) return;

        var existingEvent = await unitOfWork.ScrobbleRepository.GetEvent(
            Provider, ctx.User.Id, ctx.Series.Id, null, ScrobbleEventType.Review, true, ct
        );

        if (existingEvent is { IsProcessed: false })
        {
            logger.LogDebug("Overriding scrobble event for {Series} from Review Title {Title} -> {UpdatedTitle}",
                ctx.Series.Name, existingEvent.ReviewTitle, reviewTitle);

            existingEvent.ReviewTitle = reviewTitle;
            existingEvent.ReviewBody = reviewBody;
            existingEvent.IsBackFill &= ctx.IsBackfill;

            unitOfWork.ScrobbleRepository.Update(existingEvent);
            await unitOfWork.CommitAsync(ct);

            await auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventUpdated, ctx.Series.Id,
                new AuditLogScrobbleParamsDto
                {
                    Provider = Provider,
                    ScrobbleEventType = ScrobbleEventType.Review,
                    ReviewBody = reviewBody,
                    LibraryType = ctx.Series.Library.Type,
                }, AuditStatus.Info, userId: ctx.User.Id, ct: ct);

            return;
        }

        var scrobbleEvent = new ScrobbleEvent
        {
            ScrobbleEventType = ScrobbleEventType.Review,
            ScrobbleProvider = Provider,
            Format = ctx.Series.Library.Type.ConvertToPlusMediaFormat(ctx.Series.Format),
            SeriesId = ctx.Series.Id,
            LibraryId = ctx.Series.LibraryId,
            AppUserId = ctx.User.Id,
            ReviewTitle = reviewTitle,
            ReviewBody = reviewBody,
            IsBackFill = ctx.IsBackfill,
        };

        SetScrobbleIds(scrobbleEvent, ctx.Series);

        unitOfWork.ScrobbleRepository.Attach(scrobbleEvent);
        await unitOfWork.CommitAsync(ct);

        await auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventCreated, ctx.Series.Id,
            new AuditLogScrobbleParamsDto
            {
                Provider = Provider,
                ScrobbleEventType = ScrobbleEventType.Review,
                ReviewBody = reviewBody,
                LibraryType = ctx.Series.Library.Type,
            }, AuditStatus.Info, userId: ctx.User.Id, ct: ct);

        logger.LogDebug("Created new review scrobble event for {Series}", ctx.Series.Name);
    }

    public async Task ScrobbleReadingUpdate(ScrobbleUpdateContext ctx, CancellationToken ct = default)
    {
        if (!SupportedEvents.Contains(ScrobbleEventType.ChapterRead) || ctx.Chapter == null) return;

        // Series should only create scrobble events for completed chapters
        var chapterProgress = await unitOfWork.AppUserProgressRepository.GetUserProgressAsync(ctx.Chapter.Id, ctx.User.Id, ct);
        if (chapterProgress?.PagesRead != 0 && chapterProgress?.PagesRead < ctx.Chapter.Pages) return;

        var isAnyProgressOnSeries = await unitOfWork.AppUserProgressRepository.HasAnyProgressOnSeriesAsync(ctx.Series.Id, ctx.User.Id, ct);

        var volumeNumber = (int) await unitOfWork.AppUserProgressRepository.GetHighestFullyReadVolumeForSeries(ctx.Series.Id, ctx.User.Id, ct);
        var chapterNumber = await unitOfWork.AppUserProgressRepository.GetHighestFullyReadChapterForSeries(ctx.Series.Id, ctx.User.Id, ct);

        var existingEvent = await unitOfWork.ScrobbleRepository.GetEvent(
            Provider, ctx.User.Id, ctx.Series.Id, null, ScrobbleEventType.ChapterRead, true, ct
        );

        if (existingEvent is { IsProcessed: false })
        {
            if (!isAnyProgressOnSeries)
            {
                logger.LogDebug("Removing scrobble event for {Series} as there is no reading progress", ctx.Series.Name);

                unitOfWork.ScrobbleRepository.Remove(existingEvent);

                await unitOfWork.CommitAsync(ct);
                return;
            }

            var prevChapterNumber = existingEvent.ChapterNumber;
            var prevVolumeNumber = existingEvent.VolumeNumber;

            existingEvent.VolumeNumber = volumeNumber;
            existingEvent.ChapterNumber = chapterNumber;
            existingEvent.IsBackFill &= ctx.IsBackfill;

            unitOfWork.ScrobbleRepository.Update(existingEvent);
            await unitOfWork.CommitAsync(ct);

            await auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventUpdated, ctx.Series.Id,
                new AuditLogScrobbleParamsDto
                {
                    Provider = Provider,
                    ScrobbleEventType = ScrobbleEventType.ChapterRead,
                    ChapterNumber = chapterNumber,
                    VolumeNumber = volumeNumber,
                    LibraryType = ctx.Series.Library.Type,
                }, AuditStatus.Info, userId: ctx.User.Id, ct: ct);

            logger.LogDebug("Updated scrobble event for {Series} from Volume {PrevVolume} -> {Volume}, Chapter {PrevChapter} -> {Chapter}",
                ctx.Series.Name, prevVolumeNumber, volumeNumber, prevChapterNumber, chapterNumber);
            return;
        }

        // Do not create a new scrobble event if there is no progress
        if (!isAnyProgressOnSeries) return;

        var evt = new ScrobbleEvent
        {
            SeriesId = ctx.Series.Id,
            LibraryId = ctx.Series.LibraryId,
            ScrobbleEventType = ScrobbleEventType.ChapterRead,
            ScrobbleProvider = Provider,
            AppUserId = ctx.User.Id,
            VolumeNumber = volumeNumber,
            ChapterNumber = chapterNumber,
            Format = ctx.Series.Library.Type.ConvertToPlusMediaFormat(ctx.Series.Format),
            IsBackFill = ctx.IsBackfill,
        };

        SetScrobbleIds(evt, ctx.Series);

        if (evt.VolumeNumber is Parser.SpecialVolumeNumber)
        {
            // We don't process Specials because they will never match on AniList
            return;
        }

        unitOfWork.ScrobbleRepository.Attach(evt);
        await unitOfWork.CommitAsync(ct);

        await auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventCreated, ctx.Series.Id,
            new AuditLogScrobbleParamsDto
            {
                Provider = Provider,
                ScrobbleEventType = ScrobbleEventType.ChapterRead,
                ChapterNumber = chapterNumber,
                VolumeNumber = volumeNumber,
                LibraryType = ctx.Series.Library.Type,
            }, AuditStatus.Info, userId: ctx.User.Id, ct: ct);

        logger.LogDebug("Created new scrobble read event for {Series} with Volume {Volume}, Chapter {Chapter} for User: {UserId}",
            ctx.Series.Name, volumeNumber, chapterNumber, ctx.User.Id);

    }

    public async Task ScrobbleWantToReadUpdate(ScrobbleUpdateContext ctx, bool onWantToRead,
        CancellationToken ct = default)
    {
        if (!SupportedEvents.Contains(ScrobbleEventType.AddWantToRead) || !SupportedEvents.Contains(ScrobbleEventType.RemoveWantToRead)) return;

        var eventType = onWantToRead ? ScrobbleEventType.AddWantToRead : ScrobbleEventType.RemoveWantToRead;

        var existingEvents = (await unitOfWork.ScrobbleRepository.GetUserEventsForSeries(ctx.User.Id, ctx.Series.Id, ct))
            .Where(e => e.ScrobbleProvider == Provider)
            .Where(e => e.ScrobbleEventType is ScrobbleEventType.AddWantToRead or ScrobbleEventType.RemoveWantToRead);

        unitOfWork.ScrobbleRepository.Remove(existingEvents);

        var scrobbleEvent = new ScrobbleEvent
        {
            ScrobbleProvider = Provider,
            AppUserId = ctx.User.Id,
            LibraryId = ctx.Series.LibraryId,
            SeriesId = ctx.Series.Id,
            Format = ctx.Series.Library.Type.ConvertToPlusMediaFormat(ctx.Series.Format),
            ScrobbleEventType = eventType,
            IsBackFill = ctx.IsBackfill,
        };

        SetScrobbleIds(scrobbleEvent, ctx.Series);

        unitOfWork.ScrobbleRepository.Attach(scrobbleEvent);
        await unitOfWork.CommitAsync(ct);

        await auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventCreated, ctx.Series.Id,
            new AuditLogScrobbleParamsDto
            {
                Provider = Provider,
                ScrobbleEventType = eventType,
                LibraryType = ctx.Series.Library.Type,
            }, AuditStatus.Info, userId: ctx.User.Id, ct: ct);

        logger.LogDebug("Created new scrobble {Event} event for {Series} for User: {UserId}",
            eventType, ctx.Series.Name, ctx.User.Id);
    }
}
