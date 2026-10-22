using System.IO.Abstractions;
using AutoMapper;
using ExCSS;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Reading;
using Kavita.API.Store;
using Kavita.Common.Extensions;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.Constants;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Kobo;
using Kavita.Models.Entities.User;
using Kavita.Services.Builders;
using Kavita.Services.Kobo;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests.Kobo;

public class KoboSyncServiceTests(ITestOutputHelper testOutputHelper) : AbstractDbTest(testOutputHelper)
{
    private IKoboSyncService SetupSyncService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IReaderService? readerService = null,
        IBookService? bookService = null,
        IFileSystem? fileSystem = null
        )
    {
        var userContext = Substitute.For<IUserContext>();
        userContext.AuthKey().Returns("abc");


        return new KoboSyncService(
            Substitute.For<ILogger<KoboSyncService>>(),
            unitOfWork,
            mapper,
            readerService ?? Substitute.For<IReaderService>(),
            bookService ?? Substitute.For<IBookService>(),
            Substitute.For<IReadingSessionService>(),
            userContext,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IKepubifyConverter>(),
            fileSystem ?? Substitute.For<IFileSystem>()
            );
    }

    private async Task SetupCollection(IDataContext ctx)
    {
        var library = await ctx.Library.FirstAsync();

        var user = new AppUserBuilder("Amelia", "amelia@localhost")
            .WithLibrary(library)
            .WithRole(PolicyConstants.AdminRole)
            .Build();

        var series = new SeriesBuilder("Spice and Wolf")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Epub)
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder(ParserConstants.LooseLeafVolume)
                    .WithTitle("Spice and Wolf")
                    .Build())
                .Build())
            .Build();

        var collection = new AppUserCollectionBuilder("Kobo Sync")
            .Build();
        user.Collections = [collection];

        ctx.Users.Add(user);
        ctx.Series.Add(series);

        await ctx.SaveChangesAsync();

        collection.Items = [series];

        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task TestGetChangesSinceAsync_SyncCollection()
    {
        var (unitOfWork, ctx, mapper) = await CreateDatabase();
        var koboSyncService = SetupSyncService(unitOfWork, mapper);

        await SetupCollection(ctx);

        var syncPreferences = new AppUserKoboSyncPreferences
        {
            SyncCollection = 1,
        };

        var syncPoint = new KoboSyncPoint
        {
            UserId = 1,
            ChapterIds = [],
            ChapterIndex = 0,
            ChapterSyncTime = DateTime.MinValue,
            ProgressSyncTime = DateTime.MinValue,
            ProgressSyncedUntilId = 0,
            ProgressSyncedUntilIndex = 0
        };

        var (changes, exhausted) = await koboSyncService.GetChangesSinceAsync(1, syncPreferences, syncPoint, CancellationToken.None);

        Assert.Single(changes);
        Assert.True(exhausted);

        var change = changes[0];
        Assert.NotNull(change.NewEntitlement);
        Assert.Equal("1", change.NewEntitlement.BookEntitlement.Id);

        (changes, _) =  await koboSyncService.GetChangesSinceAsync(1, syncPreferences, syncPoint, CancellationToken.None);
        Assert.Empty(changes);

        var collection = await ctx.AppUserCollection.FirstAsync();
        collection.Items = [];
        await ctx.SaveChangesAsync();

        (changes, _)  = await koboSyncService.GetChangesSinceAsync(1, syncPreferences, syncPoint, CancellationToken.None);

        Assert.Single(changes);
        Assert.True(exhausted);

        var change2 = changes[0];
        Assert.NotNull(change2.ChangedEntitlement);
        Assert.Equal("1", change2.ChangedEntitlement.BookEntitlement.Id);
        Assert.True(change2.ChangedEntitlement.BookEntitlement.IsRemoved);

        var series2 = new SeriesBuilder("The Moon on a Rainy Night")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Epub)
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder(ParserConstants.LooseLeafVolume)
                    .Build())
                .Build())
            .Build();
        ctx.Series.Add(series2);
        collection.Items = [series2];
        await ctx.SaveChangesAsync();

        await Task.Delay(TimeSpan.FromSeconds(1));

        (changes, _) = await koboSyncService.GetChangesSinceAsync(1, syncPreferences, syncPoint, CancellationToken.None);

        Assert.Single(changes);
        Assert.True(exhausted);
        var change3 = changes[0];
        Assert.NotNull(change3.NewEntitlement);
        Assert.Equal("2", change3.NewEntitlement.BookEntitlement.Id);
    }
}
