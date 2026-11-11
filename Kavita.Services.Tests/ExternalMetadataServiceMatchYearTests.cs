using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Metadata;
using Kavita.API.Services.Plus;
using Kavita.API.Services.SignalR;
using Kavita.Database;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.KavitaPlus;
using Kavita.Models.DTOs.KavitaPlus.ExternalMetadata;
using Kavita.Models.DTOs.Metadata.Matching;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.Audit;
using Kavita.Services.Builders;
using Kavita.Services.Plus;
using Kavita.Services.Scanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests;

public class ExternalMetadataServiceMatchYearTests : AbstractDbTest
{
    public ExternalMetadataServiceMatchYearTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
        GlobalConfiguration.Configuration.UseInMemoryStorage();
    }

    [Fact]
    public async Task ManualMatch_SendsTheYearFromRegularChapters_NotAStaleStoredYear()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (service, kavitaPlusApiService) = CreateService(unitOfWork, mapper);
        var series = await AddSeriesWithADatedSpecial(context);

        await service.MatchSeries(new MatchSeriesDto { SeriesId = series.Id, Query = string.Empty });

        await kavitaPlusApiService.Received(1).MatchSeriesV3Async(
            Arg.Is<MatchRequestV3Dto>(r => r.Year == 2012), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AutoMatch_SendsTheYearFromRegularChapters_NotAStaleStoredYear()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (service, kavitaPlusApiService) = CreateService(unitOfWork, mapper);
        var series = await AddSeriesWithADatedSpecial(context);

        await service.TryMatchAndLoadMetadataForSeries(series.Id, LibraryType.Manga, MetadataFetchTrigger.OnDemand);

        await kavitaPlusApiService.Received(1).MatchSeriesV3Async(
            Arg.Is<MatchRequestV3Dto>(r => r.Year == 2012), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Match_KeepsALockedYear()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (service, kavitaPlusApiService) = CreateService(unitOfWork, mapper);
        var series = await AddSeriesWithADatedSpecial(context, lockYear: true);

        await service.MatchSeries(new MatchSeriesDto { SeriesId = series.Id, Query = string.Empty });

        await kavitaPlusApiService.Received(1).MatchSeriesV3Async(
            Arg.Is<MatchRequestV3Dto>(r => r.Year == 2018), Arg.Any<CancellationToken>());
    }

    private static async Task<Series> AddSeriesWithADatedSpecial(DataContext context, bool lockYear = false)
    {
        var library = await context.Library.FirstAsync(l => l.Id == 1);
        library.MetadataProvider = MetadataProvider.Mangabaka;
        context.Library.Update(library);
        await context.SaveChangesAsync();

        var series = new SeriesBuilder("Adventure Time")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder().WithReleaseYear(2018, lockYear).Build())
            .WithVolume(new VolumeBuilder(Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1").WithReleaseDate(new DateTime(2012, 4, 1)).Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())
            .WithVolume(new VolumeBuilder(Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder(Parser.DefaultChapter, "Princess and Princess")
                    .WithIsSpecial(true)
                    .WithReleaseDate(new DateTime(2018, 1, 24))
                    .Build())
                .Build())
            .Build();
        series.MangaBakaId = 0;
        series.AniListId = 0;
        series.MalId = 0;
        context.Series.Add(series);
        await context.SaveChangesAsync();

        return series;
    }

    private static (ExternalMetadataService Service, IKavitaPlusApiService KavitaPlusApiService) CreateService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        var licenseService = Substitute.For<ILicenseService>();
        licenseService.HasActiveLicense(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(true);

        var kavitaPlusApiService = Substitute.For<IKavitaPlusApiService>();
        kavitaPlusApiService.MatchSeriesV3Async(Arg.Any<MatchRequestV3Dto>(), Arg.Any<CancellationToken>())
            .Returns(KPlusResult<List<ExternalSeriesMatchDto>>.Failure("no matches for test"));

        var service = new ExternalMetadataService(unitOfWork,
            Substitute.For<ILogger<ExternalMetadataService>>(),
            mapper, licenseService, Substitute.For<IScrobblingService>(),
            Substitute.For<IEventHub>(), Substitute.For<ICoverDbService>(),
            kavitaPlusApiService, Substitute.For<IFileCacheService>(),
            Substitute.For<IKavitaPlusAuditService>());

        return (service, kavitaPlusApiService);
    }
}
