using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.ReadingLists;
using Kavita.Common.Extensions;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.ReadingLists.CBL;
using Kavita.Models.DTOs.ReadingLists.CBL.Import;
using Kavita.Models.DTOs.ReadingLists.CBL.Internal;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.ReadingList;
using Kavita.Models.Entities.ReadingLists;
using Kavita.Services.Builders;
using Kavita.Services.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests.ReadingLists;

public class CblImportServiceTests : AbstractDbTest
{
    public CblImportServiceTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

    #region Group 1: Fresh Import

    [Fact]
    public async Task ValidateList_AllMatched_ReturnsSuccess()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("simple-comic.json");

        var cbl = CblFileBuilder.Create("Test List")
            .AddBook("Fables", volume: "1", number: "1")
            .AddBook("Fables", volume: "1", number: "2")
            .AddBook("Fables", volume: "1", number: "3")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Equal(3, summary.SuccessfulInserts.Count);
        Assert.All(summary.SuccessfulInserts, r => Assert.Equal(CblImportReason.Success, r.Reason));
        Assert.Empty(summary.Results);
    }

    [Fact]
    public async Task ValidateList_PartialMatch_ReturnsPartial()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("simple-comic.json");

        var cbl = CblFileBuilder.Create("Test List")
            .AddBook("Fables", volume: "1", number: "1")
            .AddBook("Fables", volume: "1", number: "2")
            .AddBook("Fables", volume: "1", number: "99")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Partial, summary.Success);
        Assert.Equal(2, summary.SuccessfulInserts.Count);
        Assert.Single(summary.Results);
        Assert.Equal(CblImportReason.ChapterMissing, summary.Results.First().Reason);
    }

    [Fact]
    public async Task ValidateList_NoSeriesMatch_ReturnsFail()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("simple-comic.json");

        var cbl = CblFileBuilder.Create("Test List")
            .AddBook("NonExistent", volume: "1", number: "1")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Fail, summary.Success);
        Assert.Contains(summary.Results, r => r.Reason == CblImportReason.SeriesMissing);
    }

    [Fact]
    public async Task ValidateList_EmptyCbl_ReturnsFail()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("simple-comic.json");

        var cbl = CblFileBuilder.Create("Empty List").Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Fail, summary.Success);
        Assert.Contains(summary.Results, r => r.Reason == CblImportReason.EmptyFile);
    }

    [Fact]
    public async Task UpsertReadingList_CreatesNewList()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("simple-comic.json");

        var cbl = CblFileBuilder.Create("My New List")
            .AddBook("Fables", volume: "1", number: "1")
            .AddBook("Fables", volume: "1", number: "2")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var decisions = new CblImportDecisions
        {
            ItemResolutions = new Dictionary<int, CblItemDecision>(),
            SaveAsRemapRules = false
        };
        var summary = await svc.UpsertReadingList(seed.User.Id, filePath, decisions);

        Assert.False(summary.IsUpdate);

        // Verify reading list was created in DB
        var rl = await unitOfWork.ReadingListRepository.GetReadingListByTitleAsync("My New List", seed.User.Id);
        Assert.NotNull(rl);
        Assert.Equal(2, rl!.Items.Count);
        Assert.Equal(0, rl.Items.OrderBy(i => i.Order).First().Order);
        Assert.Equal(1, rl.Items.OrderBy(i => i.Order).Last().Order);
    }

    #endregion

    #region Group 2: Re-Import / Update

    [Fact]
    public async Task ValidateList_ExistingList_IsUpdateTrue()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("simple-comic.json");

        // Pre-create reading list
        var rl = new ReadingListBuilder("Test")
            .WithAppUserId(seed.User.Id)
            .Build();
        unitOfWork.ReadingListRepository.Add(rl);
        await unitOfWork.CommitAsync();

        var cbl = CblFileBuilder.Create("Test")
            .AddBook("Fables", volume: "1", number: "1")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.True(summary.IsUpdate);
    }

    [Fact]
    public async Task ValidateList_NewList_IsUpdateFalse()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("simple-comic.json");

        var cbl = CblFileBuilder.Create("Brand New List")
            .AddBook("Fables", volume: "1", number: "1")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.False(summary.IsUpdate);
    }

    [Fact]
    public async Task UpsertReadingList_UpdatesExistingList_NoDuplicates()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("simple-comic.json");

        var ids = seed.Lookup[("Fables", "1", "1")];

        // Pre-create reading list with Fables #1
        var rl = new ReadingListBuilder("Update Test")
            .WithAppUserId(seed.User.Id)
            .WithItem(new ReadingListItemBuilder(0, ids.SeriesId, ids.VolumeId, ids.ChapterId).Build())
            .Build();
        unitOfWork.ReadingListRepository.Add(rl);
        await unitOfWork.CommitAsync();

        // Upsert with #1, #2, #3
        var cbl = CblFileBuilder.Create("Update Test")
            .AddBook("Fables", volume: "1", number: "1")
            .AddBook("Fables", volume: "1", number: "2")
            .AddBook("Fables", volume: "1", number: "3")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var decisions = new CblImportDecisions
        {
            ItemResolutions = new Dictionary<int, CblItemDecision>(),
            SaveAsRemapRules = false
        };
        var summary = await svc.UpsertReadingList(seed.User.Id, filePath, decisions);

        Assert.True(summary.IsUpdate);

        var updated = await unitOfWork.ReadingListRepository.GetReadingListByTitleAsync("Update Test", seed.User.Id);
        Assert.NotNull(updated);
        Assert.Equal(3, updated!.Items.Count);
    }

    #endregion

    #region Group 3: Series-Level Remap Rules

    [Fact]
    public async Task ValidateList_SeriesRemap_MatchesViaTier0()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("simple-comic.json");

        var fablesIds = seed.Lookup[("Fables", "1", "1")];

        // Add series-level remap rule: "Fable" -> Fables series
        unitOfWork.RemapRuleRepository.Add(new ReadingListRemapRule
        {
            NormalizedCblSeriesName = "Fable".ToNormalized(),
            CblSeriesName = "Fable",
            SeriesId = fablesIds.SeriesId,
            SeriesNameAtMapping = "Fables",
            AppUserId = seed.User.Id,
            CreatedUtc = DateTime.UtcNow
        });
        await unitOfWork.CommitAsync();

        var cbl = CblFileBuilder.Create("Remap Test")
            .AddBook("Fable", volume: "1", number: "1")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Single(summary.SuccessfulInserts);
        Assert.Equal(CblMatchTier.RemapRule, summary.SuccessfulInserts.First().MatchTier);
    }

    [Fact]
    public async Task ValidateList_SeriesRemap_UserScoped()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("simple-comic.json", "user1");

        var user2 = await helper.AddUser("user2", seed.Library);

        var fablesIds = seed.Lookup[("Fables", "1", "1")];

        // Add user-scoped remap rule for user1 only
        unitOfWork.RemapRuleRepository.Add(new ReadingListRemapRule
        {
            NormalizedCblSeriesName = "Fable".ToNormalized(),
            CblSeriesName = "Fable",
            SeriesId = fablesIds.SeriesId,
            SeriesNameAtMapping = "Fables",
            AppUserId = seed.User.Id,
            CreatedUtc = DateTime.UtcNow
        });
        await unitOfWork.CommitAsync();

        var cbl = CblFileBuilder.Create("Scoped Test")
            .AddBook("Fable", volume: "1", number: "1")
            .Build();

        // User1 should match via remap
        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary1 = await svc.ValidateList(seed.User.Id, filePath);
        Assert.Equal(CblImportResult.Success, summary1.Success);

        // User2 should NOT match (no remap, and "Fable" doesn't match "Fables" exactly)
        var cbl2 = CblFileBuilder.Create("Scoped Test 2")
            .AddBook("Fable", volume: "1", number: "1")
            .Build();
        var filePath2 = helper.WriteCblToDisk(cbl2);
        var summary2 = await svc.ValidateList(user2.Id, filePath2);
        Assert.Contains(summary2.Results, r => r.Reason == CblImportReason.SeriesMissing);
    }

    [Fact]
    public async Task UpsertReadingList_SavesRemapRule()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("simple-comic.json");

        var fablesIds = seed.Lookup[("Fables", "1", "1")];

        var cbl = CblFileBuilder.Create("Remap Save Test")
            .AddBook("Fables", volume: "1", number: "1")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var decisions = new CblImportDecisions
        {
            ItemResolutions = new Dictionary<int, CblItemDecision>
            {
                [0] = new CblItemDecision
                {
                    SeriesId = fablesIds.SeriesId,
                    VolumeId = fablesIds.VolumeId,
                    ChapterId = fablesIds.ChapterId
                }
            },
            SaveAsRemapRules = true
        };
        await svc.UpsertReadingList(seed.User.Id, filePath, decisions);

        // Verify remap rule was persisted
        var rules = await unitOfWork.RemapRuleRepository.GetRuleDtosForUserAsync(seed.User.Id);
        Assert.NotEmpty(rules);
        Assert.Contains(rules, r =>
            r.NormalizedCblSeriesName == "Fables".ToNormalized() &&
            r.SeriesId == fablesIds.SeriesId);
    }

    [Fact]
    public async Task ValidateList_SeriesVolumeRemap()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("comic-multi-volume-series.json");

        var batman2014Ids = seed.Lookup[("Batman (2014)", "2014", "1")];
        var batman1994Ids = seed.Lookup[("Batman (1994)", "1994", "1")];

        // Series-only remap: "Batman" -> "Batman (2014)"
        unitOfWork.RemapRuleRepository.Add(new ReadingListRemapRule
        {
            NormalizedCblSeriesName = "Batman".ToNormalized(),
            CblSeriesName = "Batman",
            CblVolume = "2014",
            SeriesId = batman2014Ids.SeriesId,
            SeriesNameAtMapping = "Batman (2014)",
            AppUserId = seed.User.Id,
            CreatedUtc = DateTime.UtcNow
        });
        await unitOfWork.CommitAsync();

        var cbl = CblFileBuilder.Create("Remap Fallthrough Test")
            .AddBook("Batman", volume: "2014", number: "1") // Should match via remap
            .AddBook("Batman", volume: "1994", number: "1") // Should NOT match via remap — must fall through
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Equal(2, summary.SuccessfulInserts.Count);

        // First item: matched via remap rule to Batman (2014)
        var first = summary.SuccessfulInserts.First(r => r.Volume == "2014");
        Assert.Equal(CblMatchTier.RemapRule, first.MatchTier);
        Assert.Equal(batman2014Ids.SeriesId, first.SeriesId);

        // Second item: remap fell through, matched via ComicVine naming tier to Batman (1994)
        var second = summary.SuccessfulInserts.First(r => r.Volume == "1994");
        Assert.NotEqual(CblMatchTier.RemapRule, second.MatchTier);
        Assert.Equal(batman1994Ids.SeriesId, second.SeriesId);
    }

    #endregion

    #region Group 4: Issue-Level Remap Rules

    [Fact]
    public async Task ValidateList_IssueRemap_MatchesSpecificChapter()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("simple-comic.json");

        var ch2Ids = seed.Lookup[("Fables", "1", "2")];

        // Add issue-level remap rule: Fables vol=1 #2 -> specific chapter
        unitOfWork.RemapRuleRepository.Add(new ReadingListRemapRule
        {
            NormalizedCblSeriesName = "Fables".ToNormalized(),
            CblSeriesName = "Fables",
            CblVolume = "1",
            CblNumber = "2",
            SeriesId = ch2Ids.SeriesId,
            VolumeId = ch2Ids.VolumeId,
            ChapterId = ch2Ids.ChapterId,
            SeriesNameAtMapping = "Fables",
            AppUserId = seed.User.Id,
            CreatedUtc = DateTime.UtcNow
        });
        await unitOfWork.CommitAsync();

        var cbl = CblFileBuilder.Create("Issue Remap Test")
            .AddBook("Fables", volume: "1", number: "2")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Single(summary.SuccessfulInserts);
        Assert.Equal(CblMatchTier.RemapRule, summary.SuccessfulInserts.First().MatchTier);
        Assert.Equal(ch2Ids.ChapterId, summary.SuccessfulInserts.First().ChapterId);
    }

    [Fact]
    public async Task ValidateList_IssueRemap_DoesNotMatchWrongIssue()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("simple-comic.json");

        var ch2Ids = seed.Lookup[("Fables", "1", "2")];

        // Same issue-level rule for #2
        unitOfWork.RemapRuleRepository.Add(new ReadingListRemapRule
        {
            NormalizedCblSeriesName = "Fables".ToNormalized(),
            CblSeriesName = "Fables",
            CblVolume = "1",
            CblNumber = "2",
            SeriesId = ch2Ids.SeriesId,
            VolumeId = ch2Ids.VolumeId,
            ChapterId = ch2Ids.ChapterId,
            SeriesNameAtMapping = "Fables",
            AppUserId = seed.User.Id,
            CreatedUtc = DateTime.UtcNow
        });
        await unitOfWork.CommitAsync();

        // Request #3 — should NOT use the #2 rule, should fall through to name matching
        var cbl = CblFileBuilder.Create("Wrong Issue Test")
            .AddBook("Fables", volume: "1", number: "3")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Single(summary.SuccessfulInserts);
        // Should match via name, not remap rule
        Assert.NotEqual(CblMatchTier.RemapRule, summary.SuccessfulInserts.First().MatchTier);
    }

    [Fact]
    public async Task ValidateList_IssueRemap_TakesPrecedenceOverSeriesRemap()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("simple-comic.json");

        var ch2Ids = seed.Lookup[("Fables", "1", "2")];
        var fablesIds = seed.Lookup[("Fables", "1", "1")];

        // Add series-level remap (less specific)
        unitOfWork.RemapRuleRepository.Add(new ReadingListRemapRule
        {
            NormalizedCblSeriesName = "Fables".ToNormalized(),
            CblSeriesName = "Fables",
            SeriesId = fablesIds.SeriesId,
            SeriesNameAtMapping = "Fables",
            AppUserId = seed.User.Id,
            CreatedUtc = DateTime.UtcNow
        });

        // Add issue-level remap (more specific)
        unitOfWork.RemapRuleRepository.Add(new ReadingListRemapRule
        {
            NormalizedCblSeriesName = "Fables".ToNormalized(),
            CblSeriesName = "Fables",
            CblVolume = "1",
            CblNumber = "2",
            SeriesId = ch2Ids.SeriesId,
            VolumeId = ch2Ids.VolumeId,
            ChapterId = ch2Ids.ChapterId,
            SeriesNameAtMapping = "Fables",
            AppUserId = seed.User.Id,
            CreatedUtc = DateTime.UtcNow
        });
        await unitOfWork.CommitAsync();

        var cbl = CblFileBuilder.Create("Precedence Test")
            .AddBook("Fables", volume: "1", number: "2")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Single(summary.SuccessfulInserts);
        Assert.Equal(CblMatchTier.RemapRule, summary.SuccessfulInserts.First().MatchTier);
        // The issue-level rule should resolve to the exact chapter
        Assert.Equal(ch2Ids.ChapterId, summary.SuccessfulInserts.First().ChapterId);
    }

    #endregion

    #region Group 5: Age Rating Filtering

    [Fact]
    public async Task ValidateList_AgeRestriction_FiltersMatureSeries()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("rated-library.json");

        // Create a teen-restricted user
        var teenUser = await helper.AddUser("teen", seed.Library, AgeRating.Teen);

        var cbl = CblFileBuilder.Create("Age Filter Test")
            .AddBook("Fables", volume: "1", number: "1")
            .AddBook("Batman", volume: "2016", number: "1")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(teenUser.Id, filePath);

        Assert.Equal(CblImportResult.Partial, summary.Success);
        // Batman (Teen) should succeed, Fables (Mature) should be missing
        Assert.Single(summary.SuccessfulInserts);
        Assert.Equal("Batman", summary.SuccessfulInserts.First().Series);
        Assert.Contains(summary.Results, r => r.Reason == CblImportReason.SeriesMissing && r.Series == "Fables");
    }

    [Fact]
    public async Task ValidateList_AgeRestriction_UnrestrictedSeesAll()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("rated-library.json");

        var cbl = CblFileBuilder.Create("Unrestricted Test")
            .AddBook("Fables", volume: "1", number: "1")
            .AddBook("Batman", volume: "2016", number: "1")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Equal(2, summary.SuccessfulInserts.Count);
    }

    [Fact]
    public async Task ValidateList_AgeRestriction_UnknownExcluded()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);

        // Inline seed: series with Unknown age rating
        var library = new LibraryBuilder("TestLib", LibraryType.Comic)
            .WithFolderPath(new FolderPathBuilder("/data/testlib").Build())
            .Build();

        var series = new SeriesBuilder("Mystery Series")
            .WithMetadata(new SeriesMetadataBuilder()
                .WithAgeRating(AgeRating.Unknown)
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .Build())
            .Build();

        library.Series = [series];

        // Teen user who does NOT include unknowns — single user with library access
        var teenUser = new AppUserBuilder("teenuser", "teen@test.com")
            .WithLibrary(library)
            .Build();
        teenUser.AgeRestriction = AgeRating.Teen;
        teenUser.AgeRestrictionIncludeUnknowns = false;
        context.AppUser.Add(teenUser);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var cbl = CblFileBuilder.Create("Unknown Excluded Test")
            .AddBook("Mystery Series", volume: "1", number: "1")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(teenUser.Id, filePath);

        Assert.Contains(summary.Results, r => r.Reason == CblImportReason.SeriesMissing);
    }

    [Fact]
    public async Task ValidateList_AgeRestriction_UnknownIncluded()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);

        // Inline seed: series with Unknown age rating
        var library = new LibraryBuilder("TestLib", LibraryType.Comic)
            .WithFolderPath(new FolderPathBuilder("/data/testlib").Build())
            .Build();

        var series = new SeriesBuilder("Mystery Series")
            .WithMetadata(new SeriesMetadataBuilder()
                .WithAgeRating(AgeRating.Unknown)
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .Build())
            .Build();

        library.Series = [series];

        // Teen user who DOES include unknowns — single user with library access
        var teenUser = new AppUserBuilder("teenuser", "teen@test.com")
            .WithLibrary(library)
            .Build();
        teenUser.AgeRestriction = AgeRating.Teen;
        teenUser.AgeRestrictionIncludeUnknowns = true;
        context.AppUser.Add(teenUser);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var cbl = CblFileBuilder.Create("Unknown Included Test")
            .AddBook("Mystery Series", volume: "1", number: "1")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(teenUser.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Single(summary.SuccessfulInserts);
    }

    #endregion

    #region Group 6: External ID Matching

    [Fact]
    public async Task ValidateList_ExternalId_ComicVine_DirectMatch()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("rated-library.json");

        // Fables #1 has comicVineId "cv-111" in rated-library.json
        var cbl = CblFileBuilder.Create("CV Match Test")
            .AddBook("WrongSeriesName", volume: "1", number: "1",
                externalIds: [new CblExternalId { Provider = CblExternalDbProvider.ComicVine, IssueId = "cv-111" }])
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Single(summary.SuccessfulInserts);
        Assert.Equal(CblMatchTier.ExternalId, summary.SuccessfulInserts.First().MatchTier);
    }

    [Fact]
    public async Task ValidateList_ExternalId_Metron_DirectMatch()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("rated-library.json");

        // Fables #2 has metronId 222
        var cbl = CblFileBuilder.Create("Metron Match Test")
            .AddBook("WrongSeriesName", volume: "1", number: "2",
                externalIds: [new CblExternalId { Provider = CblExternalDbProvider.Metron, IssueId = "222" }])
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Single(summary.SuccessfulInserts);
        Assert.Equal(CblMatchTier.ExternalId, summary.SuccessfulInserts.First().MatchTier);
    }

    [Fact]
    public async Task ValidateList_ExternalId_NoMatch_FallsThrough()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("rated-library.json");

        // Nonexistent external ID but correct series name — should fall through to name match
        var cbl = CblFileBuilder.Create("Fallthrough Test")
            .AddBook("Fables", volume: "1", number: "1",
                externalIds: [new CblExternalId { Provider = CblExternalDbProvider.ComicVine, IssueId = "nonexistent" }])
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Single(summary.SuccessfulInserts);
        // Should NOT be ExternalId tier since the ID doesn't exist
        Assert.NotEqual(CblMatchTier.ExternalId, summary.SuccessfulInserts.First().MatchTier);
    }

    [Fact]
    public async Task ValidateList_ExternalId_WrongProvider_Ignored()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("rated-library.json");

        // Fables #1 has ComicVine "cv-111", but we provide it as Metron — should not match Tier 1
        var cbl = CblFileBuilder.Create("Wrong Provider Test")
            .AddBook("Fables", volume: "1", number: "1",
                externalIds: [new CblExternalId { Provider = CblExternalDbProvider.Metron, IssueId = "cv-111" }])
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Single(summary.SuccessfulInserts);
        // Should fall through to name matching, not ExternalId
        Assert.NotEqual(CblMatchTier.ExternalId, summary.SuccessfulInserts.First().MatchTier);
    }

    #endregion

    #region Group 7: Library Access

    [Fact]
    public async Task ValidateList_LibraryAccess_NoAccess_AllMissing()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("simple-comic.json");

        // Create a second library that user2 has access to (not the Comics library)
        var otherLib = new LibraryBuilder("Other", LibraryType.Manga)
            .WithFolderPath(new FolderPathBuilder("/data/other").Build())
            .Build();
        context.Library.Add(otherLib);
        await context.SaveChangesAsync();

        var user2 = new AppUserBuilder("user2", "user2@test.com")
            .WithLibrary(otherLib)
            .Build();
        context.AppUser.Add(user2);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var cbl = CblFileBuilder.Create("No Access Test")
            .AddBook("Fables", volume: "1", number: "1")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(user2.Id, filePath);

        Assert.Equal(CblImportResult.Fail, summary.Success);
    }

    [Fact]
    public async Task ValidateList_LibraryAccess_PartialAccess()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);

        // Seed two libraries inline
        var libA = new LibraryBuilder("LibA", LibraryType.Comic)
            .WithFolderPath(new FolderPathBuilder("/data/liba").Build())
            .Build();
        libA.Series = [new SeriesBuilder("SeriesA")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .Build())
            .Build()];

        var libB = new LibraryBuilder("LibB", LibraryType.Comic)
            .WithFolderPath(new FolderPathBuilder("/data/libb").Build())
            .Build();
        libB.Series = [new SeriesBuilder("SeriesB")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .Build())
            .Build()];

        // User only has access to LibA
        var user = new AppUserBuilder("partialuser", "partial@test.com")
            .WithLibrary(libA)
            .Build();
        context.Library.Add(libB);
        context.AppUser.Add(user);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var cbl = CblFileBuilder.Create("Partial Access Test")
            .AddBook("SeriesA", volume: "1", number: "1")
            .AddBook("SeriesB", volume: "1", number: "1")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(user.Id, filePath);

        Assert.Equal(CblImportResult.Partial, summary.Success);
        Assert.Single(summary.SuccessfulInserts);
        Assert.Equal("SeriesA", summary.SuccessfulInserts.First().Series);
    }

    #endregion

    #region Group 8: AlternateSeries

    [Fact]
    public async Task ValidateList_AlternateSeries_MatchesTier6()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);

        // Inline seed: series "Fables Deluxe" with chapter having AlternateSeries = "Fables"
        var library = new LibraryBuilder("Comics", LibraryType.Comic)
            .WithFolderPath(new FolderPathBuilder("/data/comics").Build())
            .Build();

        var chapter = new ChapterBuilder("1").Build();
        chapter.AlternateSeries = "Fables";

        var series = new SeriesBuilder("Fables Deluxe")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(chapter)
                .Build())
            .Build();

        library.Series = [series];

        var user = new AppUserBuilder("altuser", "alt@test.com")
            .WithLibrary(library)
            .Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        // CBL asks for "Fables" — no direct series match for "Fables Deluxe", but AlternateSeries should match
        var cbl = CblFileBuilder.Create("AlternateSeries Test")
            .AddBook("Fables", volume: "1", number: "1")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(user.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Single(summary.SuccessfulInserts);
        Assert.Equal(CblMatchTier.AlternateSeries, summary.SuccessfulInserts.First().MatchTier);
    }

    #endregion

    #region Group 9: Volume-Level Remap Rules

    [Fact]
    public async Task ValidateList_VolumeRemap_MatchesCorrectSeriesForMatchingVolume()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("comic-multi-volume-series.json");

        var batman2014Ids = seed.Lookup[("Batman (2014)", "2014", "1")];

        // Volume-only remap: "Batman" vol 2014 -> Batman (2014) series
        unitOfWork.RemapRuleRepository.Add(new ReadingListRemapRule
        {
            NormalizedCblSeriesName = "Batman".ToNormalized(),
            CblSeriesName = "Batman",
            CblVolume = "2014",
            SeriesId = batman2014Ids.SeriesId,
            SeriesNameAtMapping = "Batman (2014)",
            AppUserId = seed.User.Id,
            CreatedUtc = DateTime.UtcNow
        });
        await unitOfWork.CommitAsync();

        var cbl = CblFileBuilder.Create("Volume Remap Test")
            .AddBook("Batman", volume: "2014", number: "1")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Single(summary.SuccessfulInserts);
        Assert.Equal(CblMatchTier.RemapRule, summary.SuccessfulInserts.First().MatchTier);
        Assert.Equal(batman2014Ids.SeriesId, summary.SuccessfulInserts.First().SeriesId);
    }

    [Fact]
    public async Task ValidateList_VolumeRemap_FallsThroughForNonMatchingVolume()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("comic-multi-volume-series.json");

        var batman2014Ids = seed.Lookup[("Batman (2014)", "2014", "1")];
        var batman1994Ids = seed.Lookup[("Batman (1994)", "1994", "1")];

        // Volume-only remap: "Batman" vol 2014 -> Batman (2014)
        unitOfWork.RemapRuleRepository.Add(new ReadingListRemapRule
        {
            NormalizedCblSeriesName = "Batman".ToNormalized(),
            CblSeriesName = "Batman",
            CblVolume = "2014",
            SeriesId = batman2014Ids.SeriesId,
            SeriesNameAtMapping = "Batman (2014)",
            AppUserId = seed.User.Id,
            CreatedUtc = DateTime.UtcNow
        });
        await unitOfWork.CommitAsync();

        // Request vol 1994 — should NOT match the vol 2014 remap, should fall through
        var cbl = CblFileBuilder.Create("Volume Remap Fallthrough")
            .AddBook("Batman", volume: "1994", number: "1")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Single(summary.SuccessfulInserts);
        Assert.NotEqual(CblMatchTier.RemapRule, summary.SuccessfulInserts.First().MatchTier);
        Assert.Equal(batman1994Ids.SeriesId, summary.SuccessfulInserts.First().SeriesId);
    }

    [Fact]
    public async Task ValidateList_VolumeRemap_WithTargetVolumeId_ResolvesChaptersInOverrideVolume()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("comic-multi-volume-series.json");

        var batman2014Ids = seed.Lookup[("Batman (2014)", "2014", "2")];

        // Volume-only remap with target VolumeId: "Batman" vol 2016 -> Batman (2014) vol 2014
        unitOfWork.RemapRuleRepository.Add(new ReadingListRemapRule
        {
            NormalizedCblSeriesName = "Batman".ToNormalized(),
            CblSeriesName = "Batman",
            CblVolume = "2016",
            SeriesId = batman2014Ids.SeriesId,
            VolumeId = batman2014Ids.VolumeId,
            SeriesNameAtMapping = "Batman (2014)",
            AppUserId = seed.User.Id,
            CreatedUtc = DateTime.UtcNow
        });
        await unitOfWork.CommitAsync();

        var cbl = CblFileBuilder.Create("Volume Override Test")
            .AddBook("Batman", volume: "2016", number: "2")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Single(summary.SuccessfulInserts);
        Assert.Equal(CblMatchTier.RemapRule, summary.SuccessfulInserts.First().MatchTier);
        Assert.Equal(batman2014Ids.SeriesId, summary.SuccessfulInserts.First().SeriesId);
        Assert.Equal(batman2014Ids.ChapterId, summary.SuccessfulInserts.First().ChapterId);
    }

    [Fact]
    public async Task ValidateList_VolumeAndIssueRemap_TakesPrecedenceOverVolumeOnly()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("comic-multi-volume-series.json");

        var batman2014Ch1 = seed.Lookup[("Batman (2014)", "2014", "1")];
        var batman2014Ch2 = seed.Lookup[("Batman (2014)", "2014", "2")];

        // Volume-only remap (less specific)
        unitOfWork.RemapRuleRepository.Add(new ReadingListRemapRule
        {
            NormalizedCblSeriesName = "Batman".ToNormalized(),
            CblSeriesName = "Batman",
            CblVolume = "2014",
            SeriesId = batman2014Ch1.SeriesId,
            SeriesNameAtMapping = "Batman (2014)",
            AppUserId = seed.User.Id,
            CreatedUtc = DateTime.UtcNow
        });

        // Volume+issue remap (more specific)
        unitOfWork.RemapRuleRepository.Add(new ReadingListRemapRule
        {
            NormalizedCblSeriesName = "Batman".ToNormalized(),
            CblSeriesName = "Batman",
            CblVolume = "2014",
            CblNumber = "2",
            SeriesId = batman2014Ch2.SeriesId,
            VolumeId = batman2014Ch2.VolumeId,
            ChapterId = batman2014Ch2.ChapterId,
            SeriesNameAtMapping = "Batman (2014)",
            AppUserId = seed.User.Id,
            CreatedUtc = DateTime.UtcNow
        });
        await unitOfWork.CommitAsync();

        var cbl = CblFileBuilder.Create("Vol+Issue Precedence Test")
            .AddBook("Batman", volume: "2014", number: "2")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Single(summary.SuccessfulInserts);
        Assert.Equal(CblMatchTier.RemapRule, summary.SuccessfulInserts.First().MatchTier);
        // Volume+issue should win — specific chapter
        Assert.Equal(batman2014Ch2.ChapterId, summary.SuccessfulInserts.First().ChapterId);
    }

    [Fact]
    public async Task ValidateList_VolumeOnlyRemap_TakesPrecedenceOverSeriesOnly()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("comic-multi-volume-series.json");

        var batman2014Ids = seed.Lookup[("Batman (2014)", "2014", "1")];
        var batman1994Ids = seed.Lookup[("Batman (1994)", "1994", "1")];

        // Series-only remap: "Batman" -> Batman (1994)
        unitOfWork.RemapRuleRepository.Add(new ReadingListRemapRule
        {
            NormalizedCblSeriesName = "Batman".ToNormalized(),
            CblSeriesName = "Batman",
            SeriesId = batman1994Ids.SeriesId,
            SeriesNameAtMapping = "Batman (1994)",
            AppUserId = seed.User.Id,
            CreatedUtc = DateTime.UtcNow
        });

        // Volume-only remap: "Batman" vol 2014 -> Batman (2014)
        unitOfWork.RemapRuleRepository.Add(new ReadingListRemapRule
        {
            NormalizedCblSeriesName = "Batman".ToNormalized(),
            CblSeriesName = "Batman",
            CblVolume = "2014",
            SeriesId = batman2014Ids.SeriesId,
            SeriesNameAtMapping = "Batman (2014)",
            AppUserId = seed.User.Id,
            CreatedUtc = DateTime.UtcNow
        });
        await unitOfWork.CommitAsync();

        var cbl = CblFileBuilder.Create("Volume vs Series Precedence")
            .AddBook("Batman", volume: "2014", number: "1")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Single(summary.SuccessfulInserts);
        Assert.Equal(CblMatchTier.RemapRule, summary.SuccessfulInserts.First().MatchTier);
        // Volume-only should win over series-only
        Assert.Equal(batman2014Ids.SeriesId, summary.SuccessfulInserts.First().SeriesId);
    }

    #endregion

    #region Group 10: Series Remap — Volume Fallback to Loose-Leaf

    /// <summary>
    /// When a series-level remap targets a manga series with only loose-leaf issues,
    /// the CBL volume (e.g. "2005") doesn't exist in the target series. The matcher
    /// should fall back to loose-leaf volume and resolve the chapter there.
    /// </summary>
    [Fact]
    public async Task ValidateList_SeriesRemap_FallsBackToLooseLeafWhenVolumeMissing()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("manga-loose-leaf.json");

        var adventureTimeIds = seed.Lookup[("Adventure Time", "-100000", "1")];

        // Series-level remap: "Zombie Tales" -> Adventure Time (manga, loose-leaf only)
        unitOfWork.RemapRuleRepository.Add(new ReadingListRemapRule
        {
            NormalizedCblSeriesName = "Zombie Tales".ToNormalized(),
            CblSeriesName = "Zombie Tales",
            SeriesId = adventureTimeIds.SeriesId,
            SeriesNameAtMapping = "Adventure Time",
            AppUserId = seed.User.Id,
            CreatedUtc = DateTime.UtcNow
        });
        await unitOfWork.CommitAsync();

        // CBL has Volume="2005" which doesn't exist in Adventure Time
        var cbl = CblFileBuilder.Create("Loose Leaf Fallback Test")
            .AddBook("Zombie Tales", volume: "2005", number: "1")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Single(summary.SuccessfulInserts);
        Assert.Equal(CblMatchTier.RemapRule, summary.SuccessfulInserts.First().MatchTier);
        Assert.Equal(adventureTimeIds.SeriesId, summary.SuccessfulInserts.First().SeriesId);
    }

    /// <summary>
    /// Even with the loose-leaf fallback, if the chapter doesn't exist in the
    /// loose-leaf volume either, the result should still report failure.
    /// </summary>
    [Fact]
    public async Task ValidateList_SeriesRemap_LooseLeafFallback_ChapterMissing()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("manga-loose-leaf.json");

        var adventureTimeIds = seed.Lookup[("Adventure Time", "-100000", "1")];

        unitOfWork.RemapRuleRepository.Add(new ReadingListRemapRule
        {
            NormalizedCblSeriesName = "Zombie Tales".ToNormalized(),
            CblSeriesName = "Zombie Tales",
            SeriesId = adventureTimeIds.SeriesId,
            SeriesNameAtMapping = "Adventure Time",
            AppUserId = seed.User.Id,
            CreatedUtc = DateTime.UtcNow
        });
        await unitOfWork.CommitAsync();

        // Verify the rule was actually persisted
        var rules = await unitOfWork.RemapRuleRepository.GetRuleDtosForUserAsync(seed.User.Id);
        Assert.NotEmpty(rules);

        // Chapter 99 doesn't exist anywhere in Adventure Time
        var cbl = CblFileBuilder.Create("Missing Chapter Test")
            .AddBook("Zombie Tales", volume: "2005", number: "99")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Fail, summary.Success);
        Assert.Contains(summary.Results, r => r.Reason == CblImportReason.ChapterMissing);
    }

    /// <summary>
    /// When the series-level remap targets a Comic series where the volume DOES exist,
    /// the fallback should NOT activate — the volume should resolve directly.
    /// </summary>
    [Fact]
    public async Task ValidateList_SeriesRemap_VolumeExistsInTarget_NoFallback()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("comic-multi-volume-series.json");

        var batman2014Ids = seed.Lookup[("Batman (2014)", "2014", "1")];

        // Series-level remap: "Dark Knight" -> Batman (2014)
        unitOfWork.RemapRuleRepository.Add(new ReadingListRemapRule
        {
            NormalizedCblSeriesName = "Dark Knight".ToNormalized(),
            CblSeriesName = "Dark Knight",
            SeriesId = batman2014Ids.SeriesId,
            SeriesNameAtMapping = "Batman (2014)",
            AppUserId = seed.User.Id,
            CreatedUtc = DateTime.UtcNow
        });
        await unitOfWork.CommitAsync();

        // Volume 2014 exists in Batman (2014) — should resolve directly, no fallback needed
        var cbl = CblFileBuilder.Create("Direct Volume Match Test")
            .AddBook("Dark Knight", volume: "2014", number: "1")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Single(summary.SuccessfulInserts);
        Assert.Equal(CblMatchTier.RemapRule, summary.SuccessfulInserts.First().MatchTier);
        Assert.Equal(batman2014Ids.SeriesId, summary.SuccessfulInserts.First().SeriesId);
        Assert.Equal(batman2014Ids.ChapterId, summary.SuccessfulInserts.First().ChapterId);
    }

    /// <summary>
    /// Series-level remap with multiple CBL entries: some volumes exist in the target,
    /// some fall back to loose-leaf.
    /// </summary>
    [Fact]
    public async Task ValidateList_SeriesRemap_MultipleEntries_MixedResolution()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("manga-loose-leaf.json");

        var ch1Ids = seed.Lookup[("Adventure Time", "-100000", "1")];
        var ch3Ids = seed.Lookup[("Adventure Time", "-100000", "3")];

        unitOfWork.RemapRuleRepository.Add(new ReadingListRemapRule
        {
            NormalizedCblSeriesName = "Zombie Tales".ToNormalized(),
            CblSeriesName = "Zombie Tales",
            SeriesId = ch1Ids.SeriesId,
            SeriesNameAtMapping = "Adventure Time",
            AppUserId = seed.User.Id,
            CreatedUtc = DateTime.UtcNow
        });
        await unitOfWork.CommitAsync();

        var cbl = CblFileBuilder.Create("Mixed Resolution Test")
            .AddBook("Zombie Tales", volume: "2005", number: "1")
            .AddBook("Zombie Tales", volume: "2005", number: "3")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Equal(2, summary.SuccessfulInserts.Count);
        Assert.All(summary.SuccessfulInserts, r => Assert.Equal(CblMatchTier.RemapRule, r.MatchTier));
    }

    #endregion

    #region Group 11: Name Matching Tiers

    /// <summary>
    /// Tier 3: Comic naming pattern — "Batman" with Volume="2014" should match series "Batman (2014)"
    /// </summary>
    [Fact]
    public async Task ValidateList_ComicNamingPattern_MatchesTier3()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("comic-multi-volume-series.json");

        var batman2014Ids = seed.Lookup[("Batman (2014)", "2014", "1")];

        var cbl = CblFileBuilder.Create("Comic Naming Test")
            .AddBook("Batman", volume: "2014", number: "1")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Single(summary.SuccessfulInserts);
        Assert.Equal(CblMatchTier.ComicVineNaming, summary.SuccessfulInserts.First().MatchTier);
        Assert.Equal(batman2014Ids.SeriesId, summary.SuccessfulInserts.First().SeriesId);
    }

    /// <summary>
    /// Tier 4: Article-stripped — "The Fables" should match series "Fables" with articles removed
    /// </summary>
    [Fact]
    public async Task ValidateList_ArticleStripped_MatchesTier4()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("simple-comic.json");

        var cbl = CblFileBuilder.Create("Article Stripped Test")
            .AddBook("The Fables", volume: "1", number: "1")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Single(summary.SuccessfulInserts);
        Assert.Equal(CblMatchTier.ArticleStripped, summary.SuccessfulInserts.First().MatchTier);
    }

    /// <summary>
    /// Tier 5: Reprint-stripped — "Fables Deluxe Edition" should match "Fables" with suffix removed
    /// </summary>
    [Fact]
    public async Task ValidateList_ReprintStripped_MatchesTier5()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("simple-comic.json");

        var cbl = CblFileBuilder.Create("Reprint Stripped Test")
            .AddBook("Fables Deluxe Edition", volume: "1", number: "1")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Single(summary.SuccessfulInserts);
        Assert.Equal(CblMatchTier.ReprintStripped, summary.SuccessfulInserts.First().MatchTier);
    }

    #endregion

    #region Group 12: Global Remap Rules

    [Fact]
    public async Task ValidateList_GlobalRemap_AppliesForAnyUser()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("simple-comic.json", "user1");

        var user2 = await helper.AddUser("user2", seed.Library);

        var fablesIds = seed.Lookup[("Fables", "1", "1")];

        // Add global remap rule (created by user1, visible to all)
        unitOfWork.RemapRuleRepository.Add(new ReadingListRemapRule
        {
            NormalizedCblSeriesName = "Fable".ToNormalized(),
            CblSeriesName = "Fable",
            SeriesId = fablesIds.SeriesId,
            SeriesNameAtMapping = "Fables",
            AppUserId = seed.User.Id,
            IsGlobal = true,
            CreatedUtc = DateTime.UtcNow
        });
        await unitOfWork.CommitAsync();

        var cbl = CblFileBuilder.Create("Global Remap Test")
            .AddBook("Fable", volume: "1", number: "1")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();

        // Both users should match via global remap
        var summary1 = await svc.ValidateList(seed.User.Id, filePath);
        Assert.Equal(CblImportResult.Success, summary1.Success);
        Assert.Equal(CblMatchTier.RemapRule, summary1.SuccessfulInserts.First().MatchTier);

        var cbl2 = CblFileBuilder.Create("Global Remap Test 2")
            .AddBook("Fable", volume: "1", number: "1")
            .Build();
        var filePath2 = helper.WriteCblToDisk(cbl2);
        var summary2 = await svc.ValidateList(user2.Id, filePath2);
        Assert.Equal(CblImportResult.Success, summary2.Success);
        Assert.Equal(CblMatchTier.RemapRule, summary2.SuccessfulInserts.First().MatchTier);
    }

    [Fact]
    public async Task ValidateList_UserRemap_TakesPrecedenceOverGlobal()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("simple-comic.json", "user1");

        var user2 = await helper.AddUser("user2", seed.Library);

        var fablesIds = seed.Lookup[("Fables", "1", "1")];
        var batmanIds = seed.Lookup[("Batman", "2016", "1")];

        // Global remap created by user2: "Fable" -> Batman
        unitOfWork.RemapRuleRepository.Add(new ReadingListRemapRule
        {
            NormalizedCblSeriesName = "Fable".ToNormalized(),
            CblSeriesName = "Fable",
            SeriesId = batmanIds.SeriesId,
            SeriesNameAtMapping = "Batman",
            AppUserId = user2.Id,
            IsGlobal = true,
            CreatedUtc = DateTime.UtcNow
        });

        // User-specific remap for user1: "Fable" -> Fables (should win)
        unitOfWork.RemapRuleRepository.Add(new ReadingListRemapRule
        {
            NormalizedCblSeriesName = "Fable".ToNormalized(),
            CblSeriesName = "Fable",
            SeriesId = fablesIds.SeriesId,
            SeriesNameAtMapping = "Fables",
            AppUserId = seed.User.Id,
            CreatedUtc = DateTime.UtcNow
        });
        await unitOfWork.CommitAsync();

        var cbl = CblFileBuilder.Create("User vs Global Precedence")
            .AddBook("Fable", volume: "1", number: "1")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Single(summary.SuccessfulInserts);
        // User-specific rule should win — matched to Fables, not Batman
        Assert.Equal(fablesIds.SeriesId, summary.SuccessfulInserts.First().SeriesId);
    }

    #endregion

    #region Group 13: Series Disambiguation

    /// <summary>
    /// When two series share the same name, the CBL Year field should disambiguate.
    /// </summary>
    [Fact]
    public async Task ValidateList_SeriesDisambiguation_ByYear()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);

        var library = new LibraryBuilder("Comics", LibraryType.Comic)
            .WithFolderPath(new FolderPathBuilder("/data/comics").Build())
            .Build();

        var series2000 = new SeriesBuilder("Fables")
            .WithMetadata(new SeriesMetadataBuilder().WithReleaseYear(2000).Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .Build())
            .Build();

        var series2020 = new SeriesBuilder("Fables")
            .WithMetadata(new SeriesMetadataBuilder().WithReleaseYear(2020).Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .Build())
            .Build();

        library.Series = [series2000, series2020];

        var user = new AppUserBuilder("disambiguser", "disambig@test.com")
            .WithLibrary(library)
            .Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        // CBL entry with Year="2020" should match the 2020 series
        var cbl = CblFileBuilder.Create("Disambiguation Test")
            .AddBook("Fables", volume: "1", number: "1", year: "2020")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(user.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Single(summary.SuccessfulInserts);
        Assert.Equal(series2020.Id, summary.SuccessfulInserts.First().SeriesId);
    }

    #endregion

    #region Group 14: Issue-Only Remap Rules

    /// <summary>
    /// An issue-only remap (CblNumber set, CblVolume empty) should match any CBL entry
    /// with that issue number regardless of volume.
    /// </summary>
    [Fact]
    public async Task ValidateList_IssueOnlyRemap_MatchesRegardlessOfVolume()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("simple-comic.json");

        var ch2Ids = seed.Lookup[("Fables", "1", "2")];

        // Issue-only remap: Fables #2 (any volume)
        unitOfWork.RemapRuleRepository.Add(new ReadingListRemapRule
        {
            NormalizedCblSeriesName = "Fables".ToNormalized(),
            CblSeriesName = "Fables",
            CblNumber = "2",
            SeriesId = ch2Ids.SeriesId,
            VolumeId = ch2Ids.VolumeId,
            ChapterId = ch2Ids.ChapterId,
            SeriesNameAtMapping = "Fables",
            AppUserId = seed.User.Id,
            CreatedUtc = DateTime.UtcNow
        });
        await unitOfWork.CommitAsync();

        // CBL with volume="5" (doesn't exist) but issue #2 should still match via issue-only remap
        var cbl = CblFileBuilder.Create("Issue Only Remap Test")
            .AddBook("Fables", volume: "5", number: "2")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Single(summary.SuccessfulInserts);
        Assert.Equal(CblMatchTier.RemapRule, summary.SuccessfulInserts.First().MatchTier);
        Assert.Equal(ch2Ids.ChapterId, summary.SuccessfulInserts.First().ChapterId);
    }

    #endregion

    #region Group 15: Chapter Resolution Edge Cases

    /// <summary>
    /// When no chapter number is specified in the CBL entry, should default to first chapter in volume.
    /// </summary>
    [Fact]
    public async Task ValidateList_NoChapterNumber_DefaultsToFirstChapter()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("simple-comic.json");

        var ch1Ids = seed.Lookup[("Fables", "1", "1")];

        var cbl = CblFileBuilder.Create("No Chapter Number Test")
            .AddBook("Fables", volume: "1")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Single(summary.SuccessfulInserts);
        Assert.Equal(ch1Ids.ChapterId, summary.SuccessfulInserts.First().ChapterId);
    }

    /// <summary>
    /// When no volume is specified, chapters should be searched across all volumes.
    /// </summary>
    [Fact]
    public async Task ValidateList_NoVolume_SearchesAcrossAllVolumes()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("manga-loose-leaf.json");

        // One Piece has Volume 1 (chapters 1-3) and Volume 2 (chapters 4-6)
        var ch5Ids = seed.Lookup[("One Piece", "2", "5")];

        // No volume specified, chapter 5 is in volume 2
        var cbl = CblFileBuilder.Create("Cross Volume Search Test")
            .AddBook("One Piece", number: "5")
            .Build();

        var filePath = helper.WriteCblToDisk(cbl);
        var svc = helper.CreateImportService();
        var summary = await svc.ValidateList(seed.User.Id, filePath);

        Assert.Equal(CblImportResult.Success, summary.Success);
        Assert.Single(summary.SuccessfulInserts);
        Assert.Equal(ch5Ids.ChapterId, summary.SuccessfulInserts.First().ChapterId);
    }

    #endregion

    #region Group 16: SyncReadingListAsync

    [Fact]
    public async Task SyncReadingListAsync_WithRemapRule_MatchesNewItemAndUpdatesMetadata()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        using var helper = new CblTestHelper(unitOfWork);
        var seed = await helper.SeedLibrary("simple-comic.json");

        var fablesIds = seed.Lookup[("Fables", "1", "1")];
        var batmanIds = seed.Lookup[("Batman", "2016", "1")];

        // Pre-create a syncable reading list with 1 existing item (Fables #1)
        var rl = new ReadingListBuilder("Sync Test")
            .WithAppUserId(seed.User.Id)
            .WithItem(new ReadingListItemBuilder(0, fablesIds.SeriesId, fablesIds.VolumeId, fablesIds.ChapterId).Build())
            .Build();
        rl.Provider = ReadingListProvider.Url;
        rl.SourcePath = "test/list.cbl";
        unitOfWork.ReadingListRepository.Add(rl);
        await unitOfWork.CommitAsync();

        // Add a remap rule: "Dark Knight" -> Batman series
        unitOfWork.RemapRuleRepository.Add(new ReadingListRemapRule
        {
            NormalizedCblSeriesName = "Dark Knight".ToNormalized(),
            CblSeriesName = "Dark Knight",
            SeriesId = batmanIds.SeriesId,
            SeriesNameAtMapping = "Batman",
            AppUserId = seed.User.Id,
            CreatedUtc = DateTime.UtcNow
        });
        await unitOfWork.CommitAsync();

        // Build CBL with 2 items + metadata dates
        var cbl = CblFileBuilder.Create("Sync Test")
            .AddBook("Fables", volume: "1", number: "1")
            .AddBook("Dark Knight", volume: "2016", number: "1")
            .Build();
        cbl.StartYear = 2020;
        cbl.StartMonth = 3;
        cbl.EndYear = 2021;
        cbl.EndMonth = 6;

        var cblXml = CblTestHelper.SerializeCblToXml(cbl);

        // Set up mocks
        var githubService = Substitute.For<ICblGithubService>();
        githubService.GetFileSha("test/list.cbl").Returns(Task.FromResult("new-sha"));
        githubService.GetFileContent("test/list.cbl").Returns(Task.FromResult(cblXml));

        var dirService = new DirectoryService(
            Substitute.For<ILogger<DirectoryService>>(),
            new System.IO.Abstractions.FileSystem());

        var readingListService = Substitute.For<IReadingListService>();

        var svc = helper.CreateSyncImportService(githubService, dirService, readingListService);

        // Act
        await svc.SyncReadingListAsync(seed.User.Id, rl.Id);

        // Re-fetch from DB to verify persisted state
        var synced = await unitOfWork.ReadingListRepository
            .GetReadingListByIdAsync(rl.Id, ReadingListIncludes.Items);

        Assert.NotNull(synced);
        Assert.Equal(2, synced!.Items.Count);

        // Verify item ordering and entity mapping
        var items = synced.Items.OrderBy(i => i.Order).ToList();
        Assert.Equal(fablesIds.SeriesId, items[0].SeriesId);
        Assert.Equal(fablesIds.ChapterId, items[0].ChapterId);
        Assert.Equal(batmanIds.SeriesId, items[1].SeriesId);
        Assert.Equal(batmanIds.ChapterId, items[1].ChapterId);

        // Verify metadata updated from CBL
        Assert.Equal(2020, synced.StartingYear);
        Assert.Equal(3, synced.StartingMonth);
        Assert.Equal(2021, synced.EndingYear);
        Assert.Equal(6, synced.EndingMonth);

        // Verify sync timestamps were set
        Assert.NotNull(synced.LastSyncedUtc);
        Assert.NotNull(synced.LastSyncCheckUtc);

        // Verify side effect methods were invoked
        await readingListService.Received(1).CalculateReadingListAgeRating(Arg.Any<ReadingList>());
        await readingListService.Received(1).CalculateStartAndEndDates(Arg.Any<ReadingList>());
    }

    #endregion
}
