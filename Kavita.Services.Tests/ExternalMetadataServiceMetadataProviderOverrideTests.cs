using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Metadata;
using Kavita.API.Services.Plus;
using Kavita.API.Services.SignalR;
using Kavita.Database;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.KavitaPlus;
using Kavita.Models.DTOs.KavitaPlus.ExternalMetadata;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.Audit;
using Kavita.Models.DTOs.Metadata.Matching;
using Kavita.Services.Plus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests;

/// <summary>
/// Covers that <see cref="ExternalMetadataService.TryMatchAndLoadMetadataForSeries"/> respects a Series-level
/// <see cref="Kavita.Models.Entities.Series.MetadataProviderOverride"/> when deciding whether the Series already
/// has the id it needs (<c>HasRequiredId</c>), instead of always checking against the Library's default provider.
/// </summary>
public class ExternalMetadataServiceMetadataProviderOverrideTests : AbstractDbTest
{
    public ExternalMetadataServiceMetadataProviderOverrideTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
        GlobalConfiguration.Configuration.UseInMemoryStorage();
    }

    private static (ExternalMetadataService Service, IKavitaPlusApiService KavitaPlusApiService) CreateService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        var licenseService = Substitute.For<ILicenseService>();
        licenseService.HasActiveLicense(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(true);

        var kavitaPlusApiService = Substitute.For<IKavitaPlusApiService>();

        var service = new ExternalMetadataService(unitOfWork,
            Substitute.For<ILogger<ExternalMetadataService>>(),
            mapper, licenseService, Substitute.For<IScrobblingService>(),
            Substitute.For<IEventHub>(), Substitute.For<ICoverDbService>(),
            kavitaPlusApiService, Substitute.For<IFileCacheService>(),
            Substitute.For<IKavitaPlusAuditService>());

        return (service, kavitaPlusApiService);
    }

    [Fact]
    public async Task TryMatchAndLoadMetadataForSeries_UsesOverrideId_SkipsReMatch_WhenOverrideAlreadyMatched()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (service, kavitaPlusApiService) = CreateService(unitOfWork, mapper);

        var library = await context.Library.FirstAsync(l => l.Id == 1);
        library.MetadataProvider = MetadataProvider.Mangabaka;
        context.Library.Update(library);
        await context.SaveChangesAsync();

        var series = new SeriesBuilder("Override Series")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        // Not matched against the Library's default provider (Mangabaka)...
        series.MangaBakaId = 0;
        series.AniListId = 0;
        series.MalId = 0;
        // ...but is matched against the Series' own override provider (Hardcover).
        series.MetadataProviderOverride = MetadataProvider.Hardcover;
        series.HardcoverId = 555;
        series.ExternalSeriesMetadata.ValidUntilUtc = DateTime.UtcNow.AddDays(1);
        context.Series.Add(series);
        await context.SaveChangesAsync();

        await service.TryMatchAndLoadMetadataForSeries(series.Id, LibraryType.Manga, MetadataFetchTrigger.OnDemand);

        // Already matched against the override provider (Hardcover), so no re-match against any provider should be attempted.
        await kavitaPlusApiService.DidNotReceive().MatchSeriesV3Async(Arg.Any<MatchRequestV3Dto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryMatchAndLoadMetadataForSeries_FallsBackToLibraryDefault_WhenNoOverrideSet()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (service, kavitaPlusApiService) = CreateService(unitOfWork, mapper);

        var library = await context.Library.FirstAsync(l => l.Id == 1);
        library.MetadataProvider = MetadataProvider.Mangabaka;
        context.Library.Update(library);
        await context.SaveChangesAsync();

        var series = new SeriesBuilder("Default Provider Series")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        // Not matched against the Library's default provider (Mangabaka), and no override set,
        // so a re-match against Mangabaka should be attempted.
        series.MangaBakaId = 0;
        series.AniListId = 0;
        series.MalId = 0;
        series.MetadataProviderOverride = null;
        context.Series.Add(series);
        await context.SaveChangesAsync();

        kavitaPlusApiService.MatchSeriesV3Async(Arg.Any<MatchRequestV3Dto>(), Arg.Any<CancellationToken>())
            .Returns(KPlusResult<List<ExternalSeriesMatchDto>>.Failure("no matches for test"));

        await service.TryMatchAndLoadMetadataForSeries(series.Id, LibraryType.Manga, MetadataFetchTrigger.OnDemand);

        await kavitaPlusApiService.Received(1).MatchSeriesV3Async(
            Arg.Is<MatchRequestV3Dto>(r => r.Provider == MetadataProvider.Mangabaka), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateSeriesMetadataProviderOverride_InvalidatesCachedMetadata_WhenProviderChanges()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (service, _) = CreateService(unitOfWork, mapper);

        var library = await context.Library.FirstAsync(l => l.Id == 1);
        library.MetadataProvider = MetadataProvider.Mangabaka;
        context.Library.Update(library);
        await context.SaveChangesAsync();

        var series = new SeriesBuilder("Cached Series")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        // Data cached from the Library's default provider (Mangabaka), and a failed match against it
        series.ExternalSeriesMetadata.ValidUntilUtc = DateTime.UtcNow.AddDays(1);
        series.IsBlacklisted = true;
        context.Series.Add(series);
        await context.SaveChangesAsync();

        await service.UpdateSeriesMetadataProviderOverride(series.Id, MetadataProvider.Hardcover);

        var updated = await context.Series
            .Include(s => s.ExternalSeriesMetadata)
            .FirstAsync(s => s.Id == series.Id);

        Assert.Equal(MetadataProvider.Hardcover, updated.MetadataProviderOverride);
        // Everything held came from Mangabaka, so it must be refetched rather than served from cache
        Assert.True(await unitOfWork.ExternalSeriesMetadataRepository.NeedsDataRefresh(series.Id));
        // A failure to match against Mangabaka says nothing about Hardcover
        Assert.False(updated.IsBlacklisted);
    }

    [Fact]
    public async Task UpdateSeriesMetadataProviderOverride_KeepsCachedMetadata_WhenEffectiveProviderIsUnchanged()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (service, _) = CreateService(unitOfWork, mapper);

        var library = await context.Library.FirstAsync(l => l.Id == 1);
        library.MetadataProvider = MetadataProvider.Mangabaka;
        context.Library.Update(library);
        await context.SaveChangesAsync();

        var series = new SeriesBuilder("Pinned Series")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        series.ExternalSeriesMetadata.ValidUntilUtc = DateTime.UtcNow.AddDays(1);
        context.Series.Add(series);
        await context.SaveChangesAsync();

        // Pinning the Library's current default doesn't change who we match against, so it's not stored as an override
        await service.UpdateSeriesMetadataProviderOverride(series.Id, MetadataProvider.Mangabaka);

        var updated = await context.Series.FirstAsync(s => s.Id == series.Id);

        Assert.Null(updated.MetadataProviderOverride);
        Assert.False(await unitOfWork.ExternalSeriesMetadataRepository.NeedsDataRefresh(series.Id));
    }
}
