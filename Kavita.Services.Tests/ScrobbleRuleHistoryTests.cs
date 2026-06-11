using System;
using System.Linq;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.Database;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.KavitaPlus.Scrobble;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.KavitaPlus;
using Kavita.Models.Entities.Enums.UserPreferences;
using Kavita.Models.Entities.History;
using Kavita.Models.Entities.Progress;
using Kavita.Models.Entities.Scrobble;
using Kavita.Services.Builders;
using Kavita.Services.Plus.ScrobbleService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Kavita.Services.Tests;

/// <summary>
/// DB-backed tests for the ScrobbleRuleHistory ledger: the guard's delivered-key lookup, the read-reset,
/// provider purge, and the delivery-time upsert.
/// </summary>
public class ScrobbleRuleHistoryTests(ITestOutputHelper outputHelper) : AbstractDbTest(outputHelper)
{
    private const string Hash1 = "HASH1";
    private const string Hash2 = "HASH2";

    private static ScrobbleRuleService Sut(IUnitOfWork unitOfWork)
        => new(unitOfWork, NullLogger<ScrobbleRuleService>.Instance);

    private async Task<(int UserId, int SeriesId, int ChapterId, int VolumeId, int LibraryId)> SeedUserAndSeries(
        IUnitOfWork unitOfWork, DataContext context)
    {
        var series = new SeriesBuilder("Test Series")
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapters([new ChapterBuilder("1").WithPages(100).Build()])
                .Build())
            .Build();

        var library = new LibraryBuilder("Test Library", LibraryType.Manga)
            .WithAllowScrobbling(true)
            .WithSeries(series)
            .Build();
        context.Library.Add(library);

        var user = new AppUserBuilder("testuser", "testuser").Build();
        unitOfWork.UserRepository.Add(user);

        await unitOfWork.CommitAsync();

        var volume = series.Volumes.First();
        var chapter = volume.Chapters.First();
        return (user.Id, series.Id, chapter.Id, volume.Id, library.Id);
    }

    private static ScrobbleRuleHistory Row(int userId, int seriesId, int? chapterId, string hash, DateTime createdUtc,
        TransitionRuleKind kind = TransitionRuleKind.Inactive, ScrobbleProvider provider = ScrobbleProvider.AniList)
        => new()
        {
            AppUserId = userId,
            Provider = provider,
            RuleKind = kind,
            SeriesId = seriesId,
            ChapterId = chapterId,
            RuleHash = hash,
            CreatedUtc = createdUtc,
        };

    [Fact]
    public async Task GetDeliveredKeysAsync_ReturnsOnlyMatchingHash()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (userId, seriesId, _, _, _) = await SeedUserAndSeries(unitOfWork, context);

        context.ScrobbleRuleHistory.Add(Row(userId, seriesId, null, Hash1, DateTime.UtcNow));
        await context.SaveChangesAsync();

        var matching = await Sut(unitOfWork).GetDeliveredKeysAsync(userId, ScrobbleProvider.AniList, TransitionRuleKind.Inactive, Hash1);
        var mismatched = await Sut(unitOfWork).GetDeliveredKeysAsync(userId, ScrobbleProvider.AniList, TransitionRuleKind.Inactive, Hash2);

        Assert.Contains((seriesId, (int?)null), matching);
        Assert.Empty(mismatched);
    }

    [Fact]
    public async Task ResetReadSeriesAsync_RemovesRowsReadSinceDelivery_KeepsUnread()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (userId, seriesId, chapterId, volumeId, libraryId) = await SeedUserAndSeries(unitOfWork, context);

        // Row delivered 10 days ago
        context.ScrobbleRuleHistory.Add(Row(userId, seriesId, null, Hash1, DateTime.UtcNow.AddDays(-10)));
        await context.SaveChangesAsync();

        // No progress yet -> row should survive the reset
        await Sut(unitOfWork).ResetReadSeriesAsync(userId);
        Assert.Equal(1, await context.ScrobbleRuleHistory.CountAsync());

        // User reads the series now (LastModifiedUtc set to now by the tracker) -> newer than delivery
        context.AppUserProgresses.Add(new AppUserProgress
        {
            AppUserId = userId, SeriesId = seriesId, ChapterId = chapterId, VolumeId = volumeId,
            LibraryId = libraryId, PagesRead = 50,
        });
        await context.SaveChangesAsync();

        await Sut(unitOfWork).ResetReadSeriesAsync(userId);
        Assert.Equal(0, await context.ScrobbleRuleHistory.CountAsync());
    }

    [Fact]
    public async Task PurgeForProviderAsync_RemovesOnlyThatProvider()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (userId, seriesId, _, _, _) = await SeedUserAndSeries(unitOfWork, context);

        context.ScrobbleRuleHistory.Add(Row(userId, seriesId, null, Hash1, DateTime.UtcNow, provider: ScrobbleProvider.AniList));
        context.ScrobbleRuleHistory.Add(Row(userId, seriesId, null, Hash1, DateTime.UtcNow, provider: ScrobbleProvider.MangaBaka));
        await context.SaveChangesAsync();

        await Sut(unitOfWork).PurgeForProviderAsync(userId, ScrobbleProvider.AniList);

        var remaining = await context.ScrobbleRuleHistory.ToListAsync();
        Assert.Single(remaining);
        Assert.Equal(ScrobbleProvider.MangaBaka, remaining[0].Provider);
    }

    [Fact]
    public async Task RecordDeliveredAsync_WritesLedgerRowFromEvent()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (userId, seriesId, _, _, libraryId) = await SeedUserAndSeries(unitOfWork, context);

        var evt = new ScrobbleEvent
        {
            ScrobbleEventType = ScrobbleEventType.ReadStatusUpdate,
            ScrobbleProvider = ScrobbleProvider.AniList,
            Format = PlusMediaFormat.Manga,
            SeriesId = seriesId,
            LibraryId = libraryId,
            AppUserId = userId,
            ReadStatus = ScrobbleReadStatus.OnHold,
            TransitionRuleKind = TransitionRuleKind.Inactive,
            RuleHashSnapshot = Hash1,
        };
        unitOfWork.ScrobbleRepository.Attach(evt);
        await unitOfWork.CommitAsync();

        await Sut(unitOfWork).RecordDeliveredAsync(evt);
        await unitOfWork.CommitAsync();

        var row = await context.ScrobbleRuleHistory.SingleAsync();
        Assert.Equal(userId, row.AppUserId);
        Assert.Equal(seriesId, row.SeriesId);
        Assert.Equal(TransitionRuleKind.Inactive, row.RuleKind);
        Assert.Equal(Hash1, row.RuleHash);
        Assert.Equal(evt.Id, row.ScrobbleEventId);
    }

    [Fact]
    public async Task RecordDeliveredAsync_Twice_UpsertsSingleRow_ForNullChapterKey()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (userId, seriesId, _, _, libraryId) = await SeedUserAndSeries(unitOfWork, context);

        async Task Deliver(string hash)
        {
            var evt = new ScrobbleEvent
            {
                ScrobbleEventType = ScrobbleEventType.ReadStatusUpdate,
                ScrobbleProvider = ScrobbleProvider.AniList,
                Format = PlusMediaFormat.Manga,
                SeriesId = seriesId,
                LibraryId = libraryId,
                AppUserId = userId,
                ReadStatus = ScrobbleReadStatus.OnHold,
                TransitionRuleKind = TransitionRuleKind.Inactive,
                RuleHashSnapshot = hash,
            };
            unitOfWork.ScrobbleRepository.Attach(evt);
            await unitOfWork.CommitAsync();

            await Sut(unitOfWork).RecordDeliveredAsync(evt);
            await unitOfWork.CommitAsync();
        }

        await Deliver(Hash1);
        await Deliver(Hash2); // same (series, null-chapter) key -> must update, not insert a second row

        var rows = await context.ScrobbleRuleHistory.ToListAsync();
        Assert.Single(rows);
        Assert.Equal(Hash2, rows[0].RuleHash);
    }

    [Fact]
    public async Task PurgeStaleForSettingsAsync_DropsStaleQueuedEvents_KeepsCurrentAndProcessed()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (userId, seriesId, _, _, libraryId) = await SeedUserAndSeries(unitOfWork, context);
        var sut = Sut(unitOfWork);

        var currentRule = new ReadStatusTransitionRule
        {
            Enabled = true, Days = 30, TransitionStatus = ScrobbleReadStatus.OnHold, ExcludedPublicationStatus = [],
        };
        var currentHash = sut.ComputeHash(currentRule);
        var staleHash = sut.ComputeHash(currentRule with { Days = 14 });

        void AddEvent(string hash, bool processed) => context.ScrobbleEvent.Add(new ScrobbleEvent
        {
            ScrobbleEventType = ScrobbleEventType.ReadStatusUpdate,
            ScrobbleProvider = ScrobbleProvider.AniList,
            Format = PlusMediaFormat.Manga,
            SeriesId = seriesId,
            LibraryId = libraryId,
            AppUserId = userId,
            ReadStatus = ScrobbleReadStatus.OnHold,
            TransitionRuleKind = TransitionRuleKind.Inactive,
            RuleHashSnapshot = hash,
            IsProcessed = processed,
        });

        AddEvent(staleHash, processed: false);   // queued under old config -> drop
        AddEvent(currentHash, processed: false); // queued under current config -> keep
        AddEvent(staleHash, processed: true);    // already delivered -> keep
        await context.SaveChangesAsync();

        var settings = new ScrobbleProviderSettingsDto { InactiveSeriesRule = currentRule };
        await sut.PurgeStaleForSettingsAsync(userId, ScrobbleProvider.AniList, settings);

        var remaining = await context.ScrobbleEvent.ToListAsync();
        Assert.Equal(2, remaining.Count);
        Assert.DoesNotContain(remaining, e => !e.IsProcessed && e.RuleHashSnapshot == staleHash);
        Assert.Contains(remaining, e => !e.IsProcessed && e.RuleHashSnapshot == currentHash);
        Assert.Contains(remaining, e => e.IsProcessed && e.RuleHashSnapshot == staleHash);
    }

    [Fact]
    public async Task PurgeStaleForSettingsAsync_DisabledRule_DropsAllQueuedEvents()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (userId, seriesId, _, _, libraryId) = await SeedUserAndSeries(unitOfWork, context);
        var sut = Sut(unitOfWork);

        var rule = new ReadStatusTransitionRule
        {
            Enabled = true, Days = 30, TransitionStatus = ScrobbleReadStatus.OnHold, ExcludedPublicationStatus = [],
        };

        // A queued event whose hash matches the (still-current) config - normally kept...
        context.ScrobbleEvent.Add(new ScrobbleEvent
        {
            ScrobbleEventType = ScrobbleEventType.ReadStatusUpdate,
            ScrobbleProvider = ScrobbleProvider.AniList,
            Format = PlusMediaFormat.Manga,
            SeriesId = seriesId,
            LibraryId = libraryId,
            AppUserId = userId,
            ReadStatus = ScrobbleReadStatus.OnHold,
            TransitionRuleKind = TransitionRuleKind.Inactive,
            RuleHashSnapshot = sut.ComputeHash(rule),
            IsProcessed = false,
        });
        await context.SaveChangesAsync();

        // ...but the rule is now disabled, so recompute yields nothing -> drop the queued event too
        var settings = new ScrobbleProviderSettingsDto { InactiveSeriesRule = rule with { Enabled = false } };
        await sut.PurgeStaleForSettingsAsync(userId, ScrobbleProvider.AniList, settings);

        Assert.Equal(0, await context.ScrobbleEvent.CountAsync());
    }

    [Fact]
    public async Task RecordDeliveredAsync_IgnoresNonRuleEvents()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (userId, seriesId, _, _, libraryId) = await SeedUserAndSeries(unitOfWork, context);

        var evt = new ScrobbleEvent
        {
            ScrobbleEventType = ScrobbleEventType.ReadStatusUpdate,
            ScrobbleProvider = ScrobbleProvider.AniList,
            Format = PlusMediaFormat.Manga,
            SeriesId = seriesId,
            LibraryId = libraryId,
            AppUserId = userId,
            // No TransitionRuleKind / RuleHashSnapshot -> not a rule event
        };
        unitOfWork.ScrobbleRepository.Attach(evt);
        await unitOfWork.CommitAsync();

        await Sut(unitOfWork).RecordDeliveredAsync(evt);
        await unitOfWork.CommitAsync();

        Assert.Equal(0, await context.ScrobbleRuleHistory.CountAsync());
    }
}
