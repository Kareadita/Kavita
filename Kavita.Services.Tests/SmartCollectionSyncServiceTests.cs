using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services.Plus;
using Kavita.API.Services.SignalR;
using Kavita.Database;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.Audit;
using Kavita.Models.Entities.Enums.KavitaPlus;
using Kavita.Models.Entities.User;
using Kavita.Models.DTOs.KavitaPlus.ExternalMetadata;
using Kavita.Services.Plus;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace Kavita.Services.Tests;

#nullable enable

/// <summary>
/// Exercises the matching/linking half of <see cref="SmartCollectionSyncService"/> (the <c>LinkSeriesToCollection</c>
/// method) without touching the Kavita+ HTTP call.
/// </summary>
public class SmartCollectionSyncServiceTests(ITestOutputHelper outputHelper) : AbstractDbTest(outputHelper)
{
    private const int UserId = 1;
    private const string CollectionTitle = "My MAL Stack";

    private IKavitaPlusAuditService _auditService = null!;

    /// <summary>
    /// Seeds a library, a user with an (initially empty) MAL collection, and the given series. Returns a service wired
    /// with mocked collaborators plus a live in-memory <see cref="IUnitOfWork"/>.
    /// </summary>
    private async Task<SmartCollectionSyncService> Setup(IUnitOfWork unitOfWork, DataContext context,
        IEnumerable<Series> series, IEnumerable<Series>? seedCollectionWith = null)
    {
        var library = new LibraryBuilder("Manga", LibraryType.Manga).Build();
        foreach (var s in series)
        {
            library.Series.Add(s);
        }
        context.Library.Add(library);
        await unitOfWork.CommitAsync();

        var collection = new AppUserCollectionBuilder(CollectionTitle)
            .WithSource(ScrobbleProvider.Mal)
            .WithSourceUrl("https://myanimelist.net/stacks/123")
            .Build();
        if (seedCollectionWith != null)
        {
            collection.Items = seedCollectionWith.ToList();
        }

        var user = new AppUserBuilder("Joe", "Joe")
            .WithLibrary(library)
            .Build();
        user.Collections = new List<AppUserCollection> { collection };
        unitOfWork.UserRepository.Add(user);

        await unitOfWork.CommitAsync();

        return CreateService(unitOfWork);
    }

    private SmartCollectionSyncService CreateService(IUnitOfWork unitOfWork)
    {
        _auditService = Substitute.For<IKavitaPlusAuditService>();

        return new SmartCollectionSyncService(unitOfWork,
            Substitute.For<ILogger<SmartCollectionSyncService>>(),
            Substitute.For<IEventHub>(),
            Substitute.For<ILicenseService>(),
            _auditService);
    }

    private async Task<AppUserCollection> GetCollection(IUnitOfWork unitOfWork)
    {
        var collections = await unitOfWork.CollectionTagRepository.GetCollectionsForUserAsync(UserId, CollectionIncludes.Series);
        return collections.First(c => c.Title == CollectionTitle);
    }

    private static SeriesCollection BuildStack(params ExternalMetadataIdsDto[] entries) => new()
    {
        Title = CollectionTitle,
        Summary = "A stack",
        TotalItems = entries.Length,
        Series = entries.ToList()
    };

    private static ExternalMetadataIdsDto Entry(string name, PlusMediaFormat format = PlusMediaFormat.Manga,
        long? malId = null, int? aniListId = null, string? localizedName = null) => new()
    {
        SeriesName = name,
        LocalizedSeriesName = localizedName,
        PlusMediaFormat = format,
        MalId = malId,
        AniListId = aniListId
    };

    #region Id matching

    [Fact]
    public async Task LinkSeriesToCollection_MatchesByMalId_EvenWhenNameDiffers()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        // Deliberately mismatched name to prove the id is what matched, not the name
        var series = new SeriesBuilder("Totally Different Local Name")
            .WithFormat(MangaFormat.Archive)
            .Build();
        series.MalId = 12345;

        var service = await Setup(unitOfWork, context, [series]);
        var collection = await GetCollection(unitOfWork);

        var stack = BuildStack(Entry("Berserk", malId: 12345));

        var result = await service.LinkSeriesToCollection(collection, stack);

        Assert.Equal(0, result.MissingCount);
        Assert.Single(collection.Items);
        Assert.Equal(series.Id, collection.Items.First().Id);
    }

    [Fact]
    public async Task LinkSeriesToCollection_MatchesByAniListId()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var series = new SeriesBuilder("Vinland Saga (scanned)")
            .WithFormat(MangaFormat.Archive)
            .Build();
        series.AniListId = 99;

        var service = await Setup(unitOfWork, context, [series]);
        var collection = await GetCollection(unitOfWork);

        var stack = BuildStack(Entry("Vinland Saga", aniListId: 99));

        var result = await service.LinkSeriesToCollection(collection, stack);

        Assert.Equal(0, result.MissingCount);
        Assert.Single(collection.Items);
        Assert.Equal(series.Id, collection.Items.First().Id);
    }

    #endregion

    #region Name matching

    [Fact]
    public async Task LinkSeriesToCollection_FallsBackToNameMatch_WhenNoExternalId()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var series = new SeriesBuilder("Naruto")
            .WithFormat(MangaFormat.Archive)
            .Build();

        var service = await Setup(unitOfWork, context, [series]);
        var collection = await GetCollection(unitOfWork);

        var stack = BuildStack(Entry("Naruto"));

        var result = await service.LinkSeriesToCollection(collection, stack);

        Assert.Equal(0, result.MissingCount);
        Assert.Single(collection.Items);
        Assert.Equal(series.Id, collection.Items.First().Id);
    }

    [Fact]
    public async Task LinkSeriesToCollection_MatchesOnLocalizedName()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var series = new SeriesBuilder("One Piece")
            .WithFormat(MangaFormat.Archive)
            .WithLocalizedName("Wan Pisu")
            .Build();

        var service = await Setup(unitOfWork, context, [series]);
        var collection = await GetCollection(unitOfWork);

        // Source only knows the localized title
        var stack = BuildStack(Entry("Wan Pisu"));

        var result = await service.LinkSeriesToCollection(collection, stack);

        Assert.Equal(0, result.MissingCount);
        Assert.Single(collection.Items);
        Assert.Equal(series.Id, collection.Items.First().Id);
    }

    [Fact]
    public async Task LinkSeriesToCollection_RespectsFormat()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        // Server has the manga (Archive); the stack entry is the light novel edition, which must not cross-match
        var series = new SeriesBuilder("Overlord")
            .WithFormat(MangaFormat.Archive)
            .Build();

        var service = await Setup(unitOfWork, context, [series]);
        var collection = await GetCollection(unitOfWork);

        var stack = BuildStack(Entry("Overlord", PlusMediaFormat.LightNovel));

        var result = await service.LinkSeriesToCollection(collection, stack);

        Assert.Equal(1, result.MissingCount);
        Assert.Empty(collection.Items);
    }

    #endregion

    #region Membership / adding

    [Fact]
    public async Task LinkSeriesToCollection_AddsSeries_WhenInServerButNotInCollection()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var series = new SeriesBuilder("Chainsaw Man")
            .WithFormat(MangaFormat.Archive)
            .Build();
        series.MalId = 777;

        var service = await Setup(unitOfWork, context, [series]);
        var collection = await GetCollection(unitOfWork);

        var stack = BuildStack(Entry("Chainsaw Man", malId: 777));

        var result = await service.LinkSeriesToCollection(collection, stack);

        Assert.Equal(0, result.MissingCount);
        Assert.Single(collection.Items);
        await _auditService.Received(1).LogCollectionAsync(
            KavitaPlusEventType.CollectionItemAdded, Arg.Any<int>(), Arg.Any<object>(),
            Arg.Any<AuditStatus>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LinkSeriesToCollection_SkipsSeries_AlreadyInCollection()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var series = new SeriesBuilder("Bleach")
            .WithFormat(MangaFormat.Archive)
            .Build();
        series.MalId = 555;

        // Pre-seed the collection with the same series
        var service = await Setup(unitOfWork, context, [series], seedCollectionWith: [series]);
        var collection = await GetCollection(unitOfWork);
        Assert.Single(collection.Items);

        var stack = BuildStack(Entry("Bleach", malId: 555));

        var result = await service.LinkSeriesToCollection(collection, stack);

        Assert.Equal(0, result.MissingCount);
        Assert.Single(collection.Items); // not duplicated
        await _auditService.DidNotReceive().LogCollectionAsync(
            KavitaPlusEventType.CollectionItemAdded, Arg.Any<int>(), Arg.Any<object>(),
            Arg.Any<AuditStatus>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LinkSeriesToCollection_DoesNotDuplicate_WhenSameSeriesLivesInAnotherLibrary()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        // The same logical series exists in two libraries as distinct rows. The collection already holds the copy from
        // the second library; the server match may resolve to the first. Dedup must be by name+format, not just Id.
        var berserkA = new SeriesBuilder("Berserk").WithFormat(MangaFormat.Archive).Build();
        var berserkB = new SeriesBuilder("Berserk").WithFormat(MangaFormat.Archive).Build();

        var lib1 = new LibraryBuilder("Manga", LibraryType.Manga).Build();
        lib1.Series.Add(berserkA);
        var lib2 = new LibraryBuilder("Manga Backups", LibraryType.Manga).Build();
        lib2.Series.Add(berserkB);
        context.Library.Add(lib1);
        context.Library.Add(lib2);
        await unitOfWork.CommitAsync();

        var collection = new AppUserCollectionBuilder(CollectionTitle)
            .WithSource(ScrobbleProvider.Mal)
            .WithSourceUrl("https://myanimelist.net/stacks/123")
            .Build();
        collection.Items = new List<Series> { berserkB };

        var user = new AppUserBuilder("Joe", "Joe")
            .WithLibrary(lib1)
            .WithLibrary(lib2)
            .Build();
        user.Collections = new List<AppUserCollection> { collection };
        unitOfWork.UserRepository.Add(user);
        await unitOfWork.CommitAsync();

        var service = CreateService(unitOfWork);
        var fetched = await GetCollection(unitOfWork);
        Assert.Single(fetched.Items);

        var stack = BuildStack(Entry("Berserk"));

        var result = await service.LinkSeriesToCollection(fetched, stack);

        Assert.Equal(0, result.MissingCount);
        Assert.Single(fetched.Items); // still just the one copy, no cross-library duplicate
        await _auditService.DidNotReceive().LogCollectionAsync(
            KavitaPlusEventType.CollectionItemAdded, Arg.Any<int>(), Arg.Any<object>(),
            Arg.Any<AuditStatus>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region Missing tracking

    [Fact]
    public async Task LinkSeriesToCollection_TracksMissing_WhenNotFoundAnywhere()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var service = await Setup(unitOfWork, context, []);
        var collection = await GetCollection(unitOfWork);

        var stack = BuildStack(Entry("Nonexistent Series", malId: 424242));

        var result = await service.LinkSeriesToCollection(collection, stack);

        Assert.Equal(1, result.MissingCount);
        Assert.Empty(collection.Items);
        Assert.Contains("424242", result.MissingSeries);
        Assert.Contains("Nonexistent Series", result.MissingSeries);
        Assert.Contains("<br/>", result.MissingSeries);
    }

    [Fact]
    public async Task LinkSeriesToCollection_HandlesEmptyName_WithoutMatching()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var service = await Setup(unitOfWork, context, []);
        var collection = await GetCollection(unitOfWork);

        var stack = BuildStack(Entry(string.Empty));

        var result = await service.LinkSeriesToCollection(collection, stack);

        Assert.Equal(1, result.MissingCount);
        Assert.Empty(collection.Items);
    }

    #endregion
}
