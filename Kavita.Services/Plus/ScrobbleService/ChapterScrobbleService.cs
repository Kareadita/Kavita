using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services.Plus;
using Kavita.Models.DTOs.KavitaPlus;
using Kavita.Models.DTOs.KavitaPlus.Scrobble;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.Audit;
using Kavita.Models.Entities.Enums.KavitaPlus;
using Kavita.Models.Entities.Enums.UserPreferences;
using Kavita.Models.Entities.Scrobble;
using Kavita.Models.Entities.User;
using Kavita.Models.Extensions;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Plus.ScrobbleService;

/// <summary>
/// A <see cref="IScrobbleProviderService"/> implementation for <see cref="ScrobbleProvider"/>'s that track data
/// based on chapters. (Hardcover)
/// </summary>
public abstract class ChapterScrobbleService<T>(ILogger<T> logger, IUnitOfWork unitOfWork, IKavitaPlusAuditService auditService): IScrobbleProviderService
where T: IScrobbleProviderService
{
    protected abstract ScrobbleProvider Provider { get; }

    protected abstract IReadOnlyList<ScrobbleEventType> SupportedEvents { get; }

    protected abstract void SetScrobbleIds(ScrobbleEvent evt, Series series, Chapter chapter);

    public abstract RateProfile RateProfile { get; }

    public abstract bool IsTokenValid(string token);

    public async Task ScrobbleReadStatusUpdates(ScrobbleUpdateContext ctx, ScrobbleReadStatus status,
        TransitionRuleKind? ruleKind = null, string? ruleHash = null, CancellationToken ct = default)
    {
        if (!SupportedEvents.Contains(ScrobbleEventType.ReadStatusUpdate) || ctx.Chapter == null) return;

        var existingEvent = await unitOfWork.ScrobbleRepository.GetEvent(
            Provider, ctx.User.Id, ctx.Series.Id, ctx.Chapter.Id, ScrobbleEventType.ReadStatusUpdate, true, ct
        );

        if (existingEvent is { IsProcessed: false })
        {
            logger.LogDebug("Overriding scrobble event for {Series} - {ChapterId} from Read Status {Status} -> {UpdatedStatus}",
                ctx.Series.Name, ctx.Chapter.Id, existingEvent.ReadStatus, status);

            existingEvent.ReadStatus = status;
            existingEvent.IsBackFill &= ctx.IsBackfill;
            existingEvent.TransitionRuleKind = ruleKind;
            existingEvent.RuleHashSnapshot = ruleHash;

            unitOfWork.ScrobbleRepository.Update(existingEvent);
            await unitOfWork.CommitAsync(ct);

            await auditService.LogChapterScrobbleAsync(KavitaPlusEventType.ScrobbleEventUpdated, ctx.Series.Id, ctx.Chapter.Id,
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
            ChapterId = ctx.Chapter.Id,
            LibraryId = ctx.Series.LibraryId,
            AppUserId = ctx.User.Id,
            ReadStatus = status,
            IsBackFill = ctx.IsBackfill,
            TransitionRuleKind = ruleKind,
            RuleHashSnapshot = ruleHash,
        };

        SetScrobbleIds(scrobbleEvent, ctx.Series, ctx.Chapter);

        unitOfWork.ScrobbleRepository.Attach(scrobbleEvent);
        await unitOfWork.CommitAsync(ct);

        await auditService.LogChapterScrobbleAsync(KavitaPlusEventType.ScrobbleEventCreated, ctx.Series.Id, ctx.Chapter.Id,
            new AuditLogScrobbleParamsDto
            {
                Provider = Provider,
                ScrobbleEventType = ScrobbleEventType.ReadStatusUpdate,
                ReadStatus = status,
                TransitionRuleKind = ruleKind,
            }, AuditStatus.Info, userId: ctx.User.Id, ct: ct);

        logger.LogDebug("Created new scrobble event for {Series} - {ChapterId} with Read Status {Status}", ctx.Series.Name, ctx.Chapter.Id, status);
    }

    public async Task ScrobbleRatingUpdate(ScrobbleUpdateContext ctx, float rating, CancellationToken ct = default)
    {
        if (!SupportedEvents.Contains(ScrobbleEventType.ScoreUpdated) || ctx.Chapter == null) return;

        var existingEvent = await unitOfWork.ScrobbleRepository.GetEvent(
            Provider, ctx.User.Id, ctx.Series.Id, ctx.Chapter.Id, ScrobbleEventType.ScoreUpdated, true, ct
            );

        if (existingEvent is { IsProcessed: false })
        {
            logger.LogDebug("Overriding scrobble event for {Series}, ChapterId: {ChapterId} from Rating {Rating} -> {UpdatedRating}",
                ctx.Series.Name, ctx.Chapter.Id, existingEvent.Rating, rating);

            existingEvent.Rating = rating;
            existingEvent.IsBackFill &= ctx.IsBackfill;

            unitOfWork.ScrobbleRepository.Update(existingEvent);
            await unitOfWork.CommitAsync(ct);

            await auditService.LogChapterScrobbleAsync(KavitaPlusEventType.ScrobbleEventUpdated, ctx.Series.Id, ctx.Chapter.Id,
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
            ChapterId = ctx.Chapter.Id,
            LibraryId = ctx.Series.LibraryId,
            AppUserId = ctx.User.Id,
            Rating = rating,
            IsBackFill = ctx.IsBackfill,
        };

        SetScrobbleIds(scrobbleEvent, ctx.Series, ctx.Chapter);

        unitOfWork.ScrobbleRepository.Attach(scrobbleEvent);
        await unitOfWork.CommitAsync(ct);

        await auditService.LogChapterScrobbleAsync(KavitaPlusEventType.ScrobbleEventCreated, ctx.Series.Id, ctx.Chapter.Id,
            new AuditLogScrobbleParamsDto
            {
                Provider = Provider,
                ScrobbleEventType = ScrobbleEventType.ScoreUpdated,
                Rating = rating,
                LibraryType = ctx.Series.Library.Type,
            }, AuditStatus.Info, userId: ctx.User.Id, ct: ct);

        logger.LogDebug("Created new scrobble event for {Series}, ChapterId: {ChapterId} with Rating {Rating}",
            ctx.Series.Name, ctx.Chapter.Id, rating);
    }

    public async Task ScrobbleReviewUpdate(ScrobbleUpdateContext ctx, string? reviewTitle, string reviewBody,
        CancellationToken ct = default)
    {
        if (!SupportedEvents.Contains(ScrobbleEventType.Review) || ctx.Chapter == null) return;

        var existingEvent = await unitOfWork.ScrobbleRepository.GetEvent(
            Provider, ctx.User.Id, ctx.Series.Id, ctx.Chapter.Id, ScrobbleEventType.Review, true, ct
        );

        if (existingEvent is { IsProcessed: false })
        {
            logger.LogDebug("Overriding scrobble event for {Series} - ChapterId: {ChapterId} from Review Title {Title} -> {UpdatedTitle}",
                ctx.Series.Name, ctx.Chapter.Id, existingEvent.ReviewTitle, reviewTitle);

            existingEvent.ReviewTitle = reviewTitle;
            existingEvent.ReviewBody = reviewBody;
            existingEvent.IsBackFill &= ctx.IsBackfill;

            unitOfWork.ScrobbleRepository.Update(existingEvent);
            await unitOfWork.CommitAsync(ct);

            await auditService.LogChapterScrobbleAsync(KavitaPlusEventType.ScrobbleEventUpdated, ctx.Series.Id, ctx.Chapter.Id,
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
            ChapterId = ctx.Chapter.Id,
            LibraryId = ctx.Series.LibraryId,
            AppUserId = ctx.User.Id,
            ReviewTitle = reviewTitle,
            ReviewBody = reviewBody,
            IsBackFill = ctx.IsBackfill,
        };

        SetScrobbleIds(scrobbleEvent, ctx.Series, ctx.Chapter);

        unitOfWork.ScrobbleRepository.Attach(scrobbleEvent);
        await unitOfWork.CommitAsync(ct);

        await auditService.LogChapterScrobbleAsync(KavitaPlusEventType.ScrobbleEventCreated, ctx.Series.Id, ctx.Chapter.Id,
            new AuditLogScrobbleParamsDto
            {
                Provider = Provider,
                ScrobbleEventType = ScrobbleEventType.Review,
                ReviewBody = reviewBody,
                LibraryType = ctx.Series.Library.Type,
            }, AuditStatus.Info, userId: ctx.User.Id, ct: ct);

        logger.LogDebug("Created new scrobble event for {Series} - ChapterId: {ChapterId} with Review Title {Title}",
            ctx.Series.Name, ctx.Chapter.Id, reviewTitle);

    }

    public async Task ScrobbleReadingUpdate(ScrobbleUpdateContext ctx, CancellationToken ct = default)
    {
        if (!SupportedEvents.Contains(ScrobbleEventType.ChapterRead) || ctx.Chapter == null) return;

        var chapterProgress = await unitOfWork.AppUserProgressRepository.GetUserProgressAsync(ctx.Chapter.Id, ctx.User.Id, ct);
        var hasAnyProgress = chapterProgress is { PagesRead: > 0 };
        var existingEvent = await unitOfWork.ScrobbleRepository.GetEvent(
            Provider, ctx.User.Id, ctx.Series.Id, ctx.Chapter.Id, ScrobbleEventType.ChapterRead, true, ct
        );

        var currentProgress = (float) Math.Ceiling((chapterProgress != null ? chapterProgress.PagesRead / (float)ctx.Chapter.Pages : 0f) * 100);

        if (existingEvent is { IsProcessed: false })
        {
            if (!hasAnyProgress)
            {
                logger.LogDebug("[{Provider}] Removing scrobble event for {Series} - ChapterId: {ChapterId} as there is no reading progress",
                    Provider, ctx.Series.Name, ctx.Chapter.Id);

                unitOfWork.ScrobbleRepository.Remove(existingEvent);

                await unitOfWork.CommitAsync(ct);
                return;
            }

            var prevProgress = existingEvent.Progress;
            existingEvent.Progress = currentProgress;
            existingEvent.IsBackFill &= ctx.IsBackfill;

            unitOfWork.ScrobbleRepository.Update(existingEvent);
            await unitOfWork.CommitAsync(ct);

            // Note that this will generate a lot of events if the book has very short pages
            // Hardcover is only enabled for Light Novel & Book libraries, so I think this should be alright
            // and is worth it having a correct view of the events
            await auditService.LogChapterScrobbleAsync(KavitaPlusEventType.ScrobbleEventUpdated, ctx.Series.Id, ctx.Chapter.Id,
                new AuditLogScrobbleParamsDto
                {
                    Provider = Provider,
                    ScrobbleEventType = ScrobbleEventType.ChapterRead,
                    PercentRead = currentProgress,
                    LibraryType = ctx.Series.Library.Type,
                }, AuditStatus.Info, userId: ctx.User.Id, ct: ct);

            logger.LogDebug("Overriding scrobble event for {Series} - ChapterId: {ChapterId} from {PrevProgress}% -> {Progress}%",
                ctx.Series.Name, ctx.Chapter.Id, prevProgress, currentProgress);
            return;
        }

        if (!hasAnyProgress) return;

        var evt = new ScrobbleEvent
        {
            ScrobbleEventType = ScrobbleEventType.ChapterRead,
            ScrobbleProvider = Provider,
            Format = ctx.Series.Library.Type.ConvertToPlusMediaFormat(ctx.Series.Format),
            SeriesId = ctx.Series.Id,
            ChapterId = ctx.Chapter.Id,
            LibraryId = ctx.Series.LibraryId,
            ChapterNumber = (int) ctx.Chapter.MaxNumber,
            VolumeNumber = ctx.Chapter.Volume?.MaxNumber,
            AppUserId = ctx.User.Id,
            Progress = currentProgress,
            IsBackFill = ctx.IsBackfill,
        };

        SetScrobbleIds(evt, ctx.Series, ctx.Chapter);

        unitOfWork.ScrobbleRepository.Attach(evt);
        await unitOfWork.CommitAsync(ct);

        await auditService.LogChapterScrobbleAsync(KavitaPlusEventType.ScrobbleEventCreated, ctx.Series.Id, ctx.Chapter.Id,
            new AuditLogScrobbleParamsDto
            {
                Provider = Provider,
                ScrobbleEventType = ScrobbleEventType.ChapterRead,
                ChapterNumber = (int) ctx.Chapter.MaxNumber,
                VolumeNumber = ctx.Chapter.Volume?.MaxNumber,
                PercentRead = currentProgress,
                LibraryType = ctx.Series.Library.Type,
            }, AuditStatus.Info, userId: ctx.User.Id, ct: ct);

        logger.LogDebug("Created new scrobble event for {Series} - ChapterId: {ChapterId} with {Progress}%",
            ctx.Series.Name, ctx.Chapter.Id, currentProgress);
    }

    public async Task ScrobbleWantToReadUpdate(ScrobbleUpdateContext ctx, bool onWantToRead,
        CancellationToken ct = default)
    {
        if (!SupportedEvents.Contains(ScrobbleEventType.AddWantToRead)
            || !SupportedEvents.Contains(ScrobbleEventType.RemoveWantToRead)
            || ctx.Chapter == null) return;

        var eventType = onWantToRead ? ScrobbleEventType.AddWantToRead : ScrobbleEventType.RemoveWantToRead;

        var existingEvents = (await unitOfWork.ScrobbleRepository.GetUserEventsForSeries(ctx.User.Id, ctx.Series.Id, ct))
            .Where(e => e.ScrobbleProvider == Provider && e.ChapterId == ctx.Chapter.Id)
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
            ChapterId = ctx.Chapter.Id,
            IsBackFill = ctx.IsBackfill,
        };

        SetScrobbleIds(scrobbleEvent, ctx.Series, ctx.Chapter);

        unitOfWork.ScrobbleRepository.Attach(scrobbleEvent);
        await unitOfWork.CommitAsync(ct);

        await auditService.LogChapterScrobbleAsync(KavitaPlusEventType.ScrobbleEventUpdated, ctx.Series.Id, ctx.Chapter.Id,
            new AuditLogScrobbleParamsDto
            {
                Provider = Provider,
                ScrobbleEventType = eventType,
                LibraryType = ctx.Series.Library.Type,
            }, AuditStatus.Info, userId: ctx.User.Id, ct: ct);

        logger.LogDebug("Created new scrobble {EventType} event for {Series} - ChapterId: {ChapterId}",
            eventType, ctx.Series.Name, ctx.Chapter.Id);

    }
}
