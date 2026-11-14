using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kavita.Database.Extensions;
using Kavita.Models.Builders;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.ReadingLists;
using Kavita.Models.Entities.User;
using Kavita.Services.Builders;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Kavita.Database.Tests.Extensions;

public class KoboQueryExtensionsTests(ITestOutputHelper testOutputHelper) : AbstractDbTest(testOutputHelper)
{
    [Fact]
    public async Task FilterByKoboSyncSettings_LibrarySync_AllLibraries_ReturnsEverySupportedChapter()
    {
        var (_, ctx, _) = await CreateDatabase();
        var data = await SeedKoboData(ctx);

        var chapters = await GetSyncedChapters(ctx, new AppUserKoboSyncPreferences
        {
            LibrarySyncEnabled = true,
            ReadingListSyncEnabled = true,
            CollectionSyncEnabled = true,
        }, data.UserId);

        AssertChapterIds([
            data.Library1EpubChapterId,
            data.Library1ArchiveChapterId,
            data.Library2EpubChapterId,
            data.ReadingListChapterId,
            data.CollectionChapterId,
            data.ManagedCollectionChapterId,
            data.UnreferencedChapterId,
        ], chapters);
    }

    [Fact]
    public async Task FilterByKoboSyncSettings_LibrarySync_SpecificLibraries_OnlyReturnsThoseLibraries()
    {
        var (_, ctx, _) = await CreateDatabase();
        var data = await SeedKoboData(ctx);

        var chapters = await GetSyncedChapters(ctx, new AppUserKoboSyncPreferences
        {
            LibrarySyncEnabled = true,
            SyncLibraries = [data.Library1Id],
        }, data.UserId);

        AssertChapterIds([
            data.Library1EpubChapterId,
            data.Library1ArchiveChapterId,
        ], chapters);
    }

    [Fact]
    public async Task FilterByKoboSyncSettings_ReadingListSync_SpecificReadingLists_OnlyReturnsThoseLists()
    {
        var (_, ctx, _) = await CreateDatabase();
        var data = await SeedKoboData(ctx);

        var chapters = await GetSyncedChapters(ctx, new AppUserKoboSyncPreferences
        {
            ReadingListSyncEnabled = true,
            SyncReadingLists = [data.UserReadingListId],
        }, data.UserId);

        AssertChapterIds([data.ReadingListChapterId], chapters);
    }

    [Fact]
    public async Task FilterByKoboSyncSettings_ReadingListSync_NoSelection_ReturnsOnlyUserReadingLists()
    {
        var (_, ctx, _) = await CreateDatabase();
        var data = await SeedKoboData(ctx);

        var chapters = await GetSyncedChapters(ctx, new AppUserKoboSyncPreferences
        {
            ReadingListSyncEnabled = true,
        }, data.UserId);

        AssertChapterIds([data.ReadingListChapterId], chapters);
    }

    [Fact]
    public async Task FilterByKoboSyncSettings_CollectionSync_SpecificCollections_OnlyReturnsThoseCollections()
    {
        var (_, ctx, _) = await CreateDatabase();
        var data = await SeedKoboData(ctx);

        var chapters = await GetSyncedChapters(ctx, new AppUserKoboSyncPreferences
        {
            CollectionSyncEnabled = true,
            SyncCollections = [data.UserCollectionId],
        }, data.UserId);

        AssertChapterIds([data.CollectionChapterId], chapters);
    }

    [Fact]
    public async Task FilterByKoboSyncSettings_CollectionSync_NoSelection_ReturnsOnlyUserCollections()
    {
        var (_, ctx, _) = await CreateDatabase();
        var data = await SeedKoboData(ctx);

        var chapters = await GetSyncedChapters(ctx, new AppUserKoboSyncPreferences
        {
            CollectionSyncEnabled = true,
        }, data.UserId);

        AssertChapterIds([data.CollectionChapterId, data.ManagedCollectionChapterId], chapters);
    }

    [Fact]
    public async Task FilterByKoboSyncSettings_ManagedCollection_ReturnsItsChapters()
    {
        var (_, ctx, _) = await CreateDatabase();
        var data = await SeedKoboData(ctx);

        var chapters = await GetSyncedChapters(ctx, new AppUserKoboSyncPreferences
        {
            ManagedCollection = data.ManagedCollectionId,
        }, data.UserId);

        AssertChapterIds([data.ManagedCollectionChapterId], chapters);
    }

    [Fact]
    public async Task FilterByKoboSyncSettings_AllSources_ReturnsUnion()
    {
        var (_, ctx, _) = await CreateDatabase();
        var data = await SeedKoboData(ctx);

        var chapters = await GetSyncedChapters(ctx, new AppUserKoboSyncPreferences
        {
            LibrarySyncEnabled = true,
            SyncLibraries = [data.Library1Id],
            ReadingListSyncEnabled = true,
            SyncReadingLists = [data.UserReadingListId],
            CollectionSyncEnabled = true,
            SyncCollections = [data.UserCollectionId],
            ManagedCollection = data.ManagedCollectionId,
        }, data.UserId);

        AssertChapterIds([
            data.Library1EpubChapterId,
            data.Library1ArchiveChapterId,
            data.ReadingListChapterId,
            data.CollectionChapterId,
            data.ManagedCollectionChapterId,
        ], chapters);
    }

    [Fact]
    public async Task FilterByKoboSyncSettings_SelectionsWithoutEnabledFlags_ReturnsEmpty()
    {
        var (_, ctx, _) = await CreateDatabase();
        var data = await SeedKoboData(ctx);

        var chapters = await GetSyncedChapters(ctx, new AppUserKoboSyncPreferences
        {
            SyncLibraries = [data.Library1Id],
            SyncReadingLists = [data.UserReadingListId],
            SyncCollections = [data.UserCollectionId],
        }, data.UserId);

        Assert.Empty(chapters);
    }

    [Fact]
    public async Task FilterByKoboSyncSettings_NoSources_ReturnsEmpty()
    {
        var (_, ctx, _) = await CreateDatabase();
        var data = await SeedKoboData(ctx);

        var chapters = await GetSyncedChapters(ctx, new AppUserKoboSyncPreferences(), data.UserId);

        Assert.Empty(chapters);
    }

    private static Task<List<Chapter>> GetSyncedChapters(DataContext ctx,
        AppUserKoboSyncPreferences preferences, int userId)
    {
        return ctx.Chapter.FilterByKoboSyncSettings(preferences, userId).ToListAsync();
    }

    private static void AssertChapterIds(IReadOnlyCollection<int> expected, IEnumerable<Chapter> actual)
    {
        Assert.Equal(expected.OrderBy(id => id), actual.Select(c => c.Id).OrderBy(id => id));
    }

    private static (Series Series, Chapter Chapter) CreateSeries(string name, int libraryId, MangaFormat format)
    {
        var chapter = new ChapterBuilder("1").Build();
        var series = new SeriesBuilder(name)
            .WithLibraryId(libraryId)
            .WithFormat(format)
            .WithVolume(new VolumeBuilder("1").WithChapter(chapter).Build())
            .Build();

        return (series, chapter);
    }

    private static ReadingListItem CreateReadingListItem(Series series, Chapter chapter)
    {
        return new ReadingListItemBuilder(0, series.Id, series.Volumes.First().Id, chapter.Id).Build();
    }

    private static async Task<KoboData> SeedKoboData(DataContext ctx)
    {
        var library1 = await ctx.Library.FirstAsync();
        var library2 = new LibraryBuilder("Books").Build();
        ctx.Library.Add(library2);
        await ctx.SaveChangesAsync();

        var user = new AppUserBuilder("amelia", "amelia@example.com").Build();
        var otherUser = new AppUserBuilder("other", "other@example.com").Build();
        ctx.AppUser.AddRange(user, otherUser);
        await ctx.SaveChangesAsync();

        var (library1Epub, library1EpubChapter) = CreateSeries("Library One EPUB", library1.Id, MangaFormat.Epub);
        var (library1Archive, library1ArchiveChapter) = CreateSeries("Library One Archive", library1.Id, MangaFormat.Archive);
        var (library1Pdf, _) = CreateSeries("Library One PDF", library1.Id, MangaFormat.Pdf);
        var (library2Epub, library2EpubChapter) = CreateSeries("Library Two EPUB", library2.Id, MangaFormat.Epub);
        var (readingListSeries, readingListChapter) = CreateSeries("Reading List Series", library2.Id, MangaFormat.Epub);
        var (collectionSeries, collectionChapter) = CreateSeries("Collection Series", library2.Id, MangaFormat.Epub);
        var (managedCollectionSeries, managedCollectionChapter) =
            CreateSeries("Managed Collection Series", library2.Id, MangaFormat.Epub);
        var (unreferencedSeries, unreferencedChapter) =
            CreateSeries("Unreferenced Series", library2.Id, MangaFormat.Epub);

        ctx.Series.AddRange(library1Epub, library1Archive, library1Pdf, library2Epub, readingListSeries,
            collectionSeries, managedCollectionSeries, unreferencedSeries);
        await ctx.SaveChangesAsync();

        var userReadingList = new ReadingListBuilder("User Reading List")
            .WithAppUserId(user.Id)
            .WithItem(CreateReadingListItem(readingListSeries, readingListChapter))
            .Build();
        var otherUserReadingList = new ReadingListBuilder("Other User Reading List")
            .WithAppUserId(otherUser.Id)
            .WithItem(CreateReadingListItem(unreferencedSeries, unreferencedChapter))
            .Build();
        ctx.ReadingList.AddRange(userReadingList, otherUserReadingList);

        var userCollection = new AppUserCollectionBuilder("User Collection").WithItem(collectionSeries).Build();
        userCollection.AppUserId = user.Id;
        var otherUserCollection = new AppUserCollectionBuilder("Other User Collection").WithItem(unreferencedSeries).Build();
        otherUserCollection.AppUserId = otherUser.Id;
        var managedCollection = new AppUserCollectionBuilder("Managed Collection").WithItem(managedCollectionSeries).Build();
        managedCollection.AppUserId = user.Id;
        ctx.AppUserCollection.AddRange(userCollection, otherUserCollection, managedCollection);

        await ctx.SaveChangesAsync();

        return new KoboData
        {
            UserId = user.Id,
            Library1Id = library1.Id,
            Library1EpubChapterId = library1EpubChapter.Id,
            Library1ArchiveChapterId = library1ArchiveChapter.Id,
            Library2EpubChapterId = library2EpubChapter.Id,
            ReadingListChapterId = readingListChapter.Id,
            CollectionChapterId = collectionChapter.Id,
            ManagedCollectionChapterId = managedCollectionChapter.Id,
            UnreferencedChapterId = unreferencedChapter.Id,
            UserReadingListId = userReadingList.Id,
            UserCollectionId = userCollection.Id,
            ManagedCollectionId = managedCollection.Id,
        };
    }

    private sealed class KoboData
    {
        public required int UserId { get; init; }
        public required int Library1Id { get; init; }
        public required int Library1EpubChapterId { get; init; }
        public required int Library1ArchiveChapterId { get; init; }
        public required int Library2EpubChapterId { get; init; }
        public required int ReadingListChapterId { get; init; }
        public required int CollectionChapterId { get; init; }
        public required int ManagedCollectionChapterId { get; init; }
        public required int UnreferencedChapterId { get; init; }
        public required int UserReadingListId { get; init; }
        public required int UserCollectionId { get; init; }
        public required int ManagedCollectionId { get; init; }
    }
}
