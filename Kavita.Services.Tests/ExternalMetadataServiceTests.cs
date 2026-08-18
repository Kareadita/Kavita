using AutoMapper;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Metadata;
using Kavita.API.Services.Plus;
using Kavita.API.Services.SignalR;
using Kavita.Common.Extensions;
using Kavita.Database;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.KavitaPlus.Metadata;
using Kavita.Models.DTOs.Recommendation;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.KavitaPlus;
using Kavita.Models.Entities.Metadata;
using Kavita.Models.Entities.MetadataMatching;
using Kavita.Models.Entities.Person;
using Kavita.Services.Builders;
using Kavita.Services.Plus;
using Kavita.Services.Scanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests;

/// <summary>
/// Given these rely on Kavita+, this will not have any [Fact]/[Theory] on them and must be manually checked
/// </summary>
public class ExternalMetadataServiceTests: AbstractDbTest
{
    public ExternalMetadataServiceTests(ITestOutputHelper outputHelper): base(outputHelper)
    {
        // Set up Hangfire to use in-memory storage for testing
        GlobalConfiguration.Configuration.UseInMemoryStorage();
    }

    private async Task<(IExternalMetadataService, Dictionary<string, Genre>, Dictionary<string, Tag>, Dictionary<string, Person>)> Setup(IUnitOfWork unitOfWork, DataContext context, IMapper mapper)
    {

       var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
       metadataSettings.Enabled = false;
       metadataSettings.EnableSummary = false;
       metadataSettings.EnableAgeRating = true;
       metadataSettings.EnableCoverImage = false;
       metadataSettings.EnableLocalizedName = false;
       metadataSettings.EnableGenres = false;
       metadataSettings.EnablePeople = false;
       metadataSettings.EnableRelationships = false;
       metadataSettings.EnableTags = false;
       metadataSettings.EnablePublicationStatus = false;
       metadataSettings.EnableStartDate = false;
       metadataSettings.FieldMappings = [];
       metadataSettings.AgeRatingMappings = new Dictionary<string, AgeRating>();
       metadataSettings.ExternalAgeRatingMappings = new Dictionary<string, AgeRating>();
       metadataSettings.FilterAboveWeight = null;
       context.MetadataSettings.Update(metadataSettings);

       await context.SaveChangesAsync();

       context.AppUser.Add(new AppUserBuilder("Joe", "Joe")
           .WithRole(PolicyConstants.AdminRole)
           .WithLibrary(await context.Library.FirstAsync(l => l.Id == 1))
           .Build());

       var genreLookup = new Dictionary<string, Genre>();
       var tagLookup = new Dictionary<string, Tag>();
       var personLookup = new Dictionary<string, Person>();

       // Create a bunch of Genres for this test and store their string in genreLookup
       genreLookup.Clear();
       var g1 = new GenreBuilder("Action").Build();
       var g2 = new GenreBuilder("Ecchi").Build();
       context.Genre.Add(g1);
       context.Genre.Add(g2);
       genreLookup.Add("Action", g1);
       genreLookup.Add("Ecchi", g2);

       tagLookup.Clear();
       var t1 = new TagBuilder("H").Build();
       var t2 = new TagBuilder("Boxing").Build();
       context.Tag.Add(t1);
       context.Tag.Add(t2);
       tagLookup.Add("H", t1);
       tagLookup.Add("Boxing", t2);

       personLookup.Clear();
       var p1 = new PersonBuilder("Johnny Twowheeler").Build();
       var p2 = new PersonBuilder("Boxing").Build();
       context.Person.Add(p1);
       context.Person.Add(p2);
       personLookup.Add("Johnny Twowheeler", p1);
       personLookup.Add("Batman Robin", p2);

       await context.SaveChangesAsync();

       var externalMetadataService = new ExternalMetadataService(unitOfWork,
           Substitute.For<ILogger<ExternalMetadataService>>(),
           mapper, Substitute.For<ILicenseService>(), Substitute.For<IScrobblingService>(),
           Substitute.For<IEventHub>(), Substitute.For<ICoverDbService>(),
           Substitute.For<IKavitaPlusApiService>(), Substitute.For<IFileCacheService>(),
           Substitute.For<IKavitaPlusAuditService>());

       // Clear tracker so test body starts with a clean slate
       context.ChangeTracker.Clear();

       return (externalMetadataService, genreLookup, tagLookup, personLookup);
    }

    #region Gloabl

    [Fact]
    public async Task Off_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Summary";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = false;
        metadataSettings.EnableSummary = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Summary = "Test"
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(string.Empty, postSeries.Metadata.Summary);
    }

    #endregion

    #region Summary

    [Fact]
    public async Task Summary_NoExisting_Off_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Summary";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableSummary = false;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Summary = "Test"
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(string.Empty, postSeries.Metadata.Summary);
    }

    [Fact]
    public async Task Summary_NoExisting_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Summary";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableSummary = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Summary = "Test"
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.False(string.IsNullOrEmpty(postSeries.Metadata.Summary));
        Assert.Equal(series.Metadata.Summary, postSeries.Metadata.Summary);
    }

    [Fact]
    public async Task Summary_Existing_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Summary";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithSummary("This summary is not locked")
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableSummary = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Summary = "This should not write"
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.False(string.IsNullOrEmpty(postSeries.Metadata.Summary));
        Assert.Equal("This summary is not locked", postSeries.Metadata.Summary);
    }

    [Fact]
    public async Task Summary_Existing_Locked_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Summary";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithSummary("This summary is not locked", true)
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableSummary = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Summary = "This should not write"
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.False(string.IsNullOrEmpty(postSeries.Metadata.Summary));
        Assert.Equal("This summary is not locked", postSeries.Metadata.Summary);
    }

    [Fact]
    public async Task Summary_Existing_Locked_Override_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Summary";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithSummary("This summary is not locked", true)
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableSummary = true;
        metadataSettings.Overrides = [MetadataSettingField.Summary];
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Summary = "This should write"
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.False(string.IsNullOrEmpty(postSeries.Metadata.Summary));
        Assert.Equal("This should write", postSeries.Metadata.Summary);
    }


    #endregion

    #region Release Year

    [Fact]
    public async Task ReleaseYear_NoExisting_Off_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Release Year";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableStartDate = false;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            StartDate = DateTime.UtcNow
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(0, postSeries.Metadata.ReleaseYear);
    }

    [Fact]
    public async Task ReleaseYear_NoExisting_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Release Year";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableStartDate = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            StartDate = DateTime.UtcNow
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(DateTime.UtcNow.Year, postSeries.Metadata.ReleaseYear);
    }

    [Fact]
    public async Task ReleaseYear_Existing_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Release Year";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithReleaseYear(1990)
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableStartDate = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            StartDate = DateTime.UtcNow
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(1990, postSeries.Metadata.ReleaseYear);
    }

    [Fact]
    public async Task ReleaseYear_Existing_Locked_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Release Year";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithReleaseYear(1990, true)
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableStartDate = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            StartDate = DateTime.UtcNow
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(1990, postSeries.Metadata.ReleaseYear);
    }

    [Fact]
    public async Task ReleaseYear_Existing_Locked_Override_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Release Year";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithReleaseYear(1990, true)
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableStartDate = true;
        metadataSettings.Overrides = [MetadataSettingField.StartDate];
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            StartDate = DateTime.UtcNow
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(DateTime.UtcNow.Year, postSeries.Metadata.ReleaseYear);
    }

    #endregion

    #region LocalizedName

    [Fact]
    public async Task LocalizedName_NoExisting_Off_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Localized Name";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithLocalizedNameAllowEmpty(string.Empty)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableLocalizedName = false;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        // A perfectly viable candidate - the setting being off is the only reason nothing is written
        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            LocalizedTitles = new Dictionary<string, IList<LocalizedTitleDto>>
            {
                ["ja-Latn"] = [new LocalizedTitleDto { Title = "Kimchi" }]
            }
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(string.Empty, postSeries.LocalizedName);
    }

    [Fact]
    public async Task LocalizedName_NoExisting_Modification()
    {
        // Default seeded priorities are Name "en" and LocalizedName "ja-Latn"
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Localized Name";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithLocalizedNameAllowEmpty(string.Empty)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableLocalizedName = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            LocalizedTitles = new Dictionary<string, IList<LocalizedTitleDto>>
            {
                ["ja-Latn"] = [new LocalizedTitleDto { Title = "Kimchi" }]
            }
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal("Kimchi", postSeries.LocalizedName);
    }

    [Fact]
    public async Task LocalizedName_Existing_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Localized Name";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithLocalizedName("Localized Name here")
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableLocalizedName = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        // A perfectly viable candidate - the existing value with no force override is why nothing is written
        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            LocalizedTitles = new Dictionary<string, IList<LocalizedTitleDto>>
            {
                ["ja-Latn"] = [new LocalizedTitleDto { Title = "Kimchi" }]
            }
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal("Localized Name here", postSeries.LocalizedName);
    }

    [Fact]
    public async Task LocalizedName_Existing_Locked_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Localized Name";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithLocalizedName("Localized Name here", true)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableLocalizedName = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        // A perfectly viable candidate - the lock with no force override is why nothing is written
        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            LocalizedTitles = new Dictionary<string, IList<LocalizedTitleDto>>
            {
                ["ja-Latn"] = [new LocalizedTitleDto { Title = "Kimchi" }]
            }
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal("Localized Name here", postSeries.LocalizedName);
    }

    [Fact]
    public async Task LocalizedName_Existing_Locked_Override_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Localized Name";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithLocalizedName("Localized Name here", true)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableLocalizedName = true;
        metadataSettings.Overrides = [MetadataSettingField.LocalizedName];
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            LocalizedTitles = new Dictionary<string, IList<LocalizedTitleDto>>
            {
                ["ja-Latn"] = [new LocalizedTitleDto { Title = "Kimchi" }]
            }
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal("Kimchi", postSeries.LocalizedName);
    }

    [Fact]
    public async Task LocalizedName_NoLocalizedTitles_NoModification()
    {
        // LocalizedTitles is the only source now. A provider that sends none gives us no way to know a synonym's
        // script, so nothing is written rather than guessing.
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Localized Name";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithLocalizedNameAllowEmpty(string.Empty)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableLocalizedName = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Synonyms = [seriesName, "設定しないでください", "Kimchi"]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.True(string.IsNullOrEmpty(postSeries.LocalizedName));
    }

    [Fact]
    public async Task LocalizedName_CandidateCollidesWithOtherSeries_PicksNextUnique()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Localized Name";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithLocalizedNameAllowEmpty(string.Empty)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);

        // Another series already carries this name; picking it as the localized name would break the scanner's
        // SingleOrDefault lookup, so it must be skipped in favour of the next unique candidate.
        var other = new SeriesBuilder("Kimchi")
            .WithLibraryId(1)
            .Build();
        context.Series.Attach(other);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableLocalizedName = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        // Retry happens WITHIN a language too - "Kimchi" collides, so we walk to ja-Latn's next title rather
        // than abandoning the language entirely
        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            LocalizedTitles = new Dictionary<string, IList<LocalizedTitleDto>>
            {
                ["ja-Latn"] =
                [
                    new LocalizedTitleDto { Title = "Kimchi" },
                    new LocalizedTitleDto { Title = "Bibimbap" }
                ]
            }
        }, 1);

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal("Bibimbap", postSeries.LocalizedName);
    }

    [Fact]
    public async Task LocalizedName_AllCandidatesCollide_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Localized Name";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithLocalizedNameAllowEmpty(string.Empty)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);

        var other = new SeriesBuilder("Kimchi")
            .WithLibraryId(1)
            .Build();
        context.Series.Attach(other);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableLocalizedName = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        // Every candidate collides with another series - nothing should be written
        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            LocalizedTitles = new Dictionary<string, IList<LocalizedTitleDto>>
            {
                ["ja-Latn"] = [new LocalizedTitleDto { Title = "Kimchi" }]
            }
        }, 1);

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.True(string.IsNullOrEmpty(postSeries.LocalizedName));
    }

    [Fact]
    public async Task LocalizedName_CandidateMatchesOwnName_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Localized Name";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithLocalizedNameAllowEmpty(string.Empty)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableLocalizedName = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        // The taken-names set excludes this series, so a candidate matching our OWN Name has to be caught by
        // the explicit self guard - otherwise LocalizedName would just duplicate Name
        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            LocalizedTitles = new Dictionary<string, IList<LocalizedTitleDto>>
            {
                ["ja-Latn"] = [new LocalizedTitleDto { Title = seriesName }]
            }
        }, 1);

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.True(string.IsNullOrEmpty(postSeries.LocalizedName));
    }

    [Fact]
    public async Task LocalizedName_SharesTopLanguageWithName_FallsToNextLanguage()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        var series = new SeriesBuilder("Original Name")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithLocalizedNameAllowEmpty(string.Empty)
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableName = true;
        metadataSettings.EnableLocalizedName = true;
        metadataSettings.GlobalNameLanguages = "en";
        metadataSettings.GlobalLocalizedNameLanguages = "en;ja-Latn";
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        // Both lists lead with "en". Name eats it, so LocalizedName has to fall through to ja-Latn.
        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = "Bleach",
            LocalizedTitles = new Dictionary<string, IList<LocalizedTitleDto>>
            {
                ["en"] = [new LocalizedTitleDto { Title = "Bleach" }],
                ["ja-Latn"] = [new LocalizedTitleDto { Title = "Burichi" }]
            }
        }, 1);

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal("Bleach", postSeries.Name);
        Assert.Equal("Burichi", postSeries.LocalizedName);
    }

    [Fact]
    public async Task LibraryOverride_BlankField_SkipsWrite()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        var series = new SeriesBuilder("Original Name")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithLocalizedNameAllowEmpty(string.Empty)
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableName = true;
        metadataSettings.EnableLocalizedName = true;
        // Global Name priority is deliberately a language the provider does not send, so a Name write proves the
        // library override won for that field. The global would happily resolve LocalizedName if it were consulted.
        metadataSettings.GlobalNameLanguages = "de";
        metadataSettings.GlobalLocalizedNameLanguages = "ja-Latn";
        metadataSettings.LibraryLanguageTitleOverrides = new Dictionary<int, SeriesNameLanguage>
        {
            [1] = new SeriesNameLanguage { Name = "en", LocalizedName = string.Empty }
        };
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = "Bleach",
            LocalizedTitles = new Dictionary<string, IList<LocalizedTitleDto>>
            {
                ["en"] = [new LocalizedTitleDto { Title = "Bleach" }],
                ["ja-Latn"] = [new LocalizedTitleDto { Title = "Burichi" }]
            }
        }, 1);

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        // The override replaces the global outright - clearing LocalizedName on it opts the field out for this
        // library rather than falling back to the global
        Assert.Equal("Bleach", postSeries.Name);
        Assert.True(string.IsNullOrEmpty(postSeries.LocalizedName));
    }

    [Fact]
    public async Task LanguageCodes_MatchCaseInsensitively()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        var series = new SeriesBuilder("Original Name")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithLocalizedNameAllowEmpty(string.Empty)
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableName = true;
        metadataSettings.EnableLocalizedName = true;
        // Admin-typed casing, versus the canonical BCP-47 casing K+ sends
        metadataSettings.GlobalNameLanguages = "EN";
        metadataSettings.GlobalLocalizedNameLanguages = "ja-latn";
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = "Bleach",
            LocalizedTitles = new Dictionary<string, IList<LocalizedTitleDto>>
            {
                ["en"] = [new LocalizedTitleDto { Title = "Bleach" }],
                ["ja-Latn"] = [new LocalizedTitleDto { Title = "Burichi" }]
            }
        }, 1);

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal("Bleach", postSeries.Name);
        Assert.Equal("Burichi", postSeries.LocalizedName);
    }

    [Fact]
    public async Task LocalizedName_NameAlreadyLocked_StillExcludesNameLanguage()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        // Simulates the second K+ run: a previous pass wrote Name from "en" and set NameLocked, so UpdateName
        // is now a no-op. The excluded language has to come from the Name the series HOLDS, not from this run's
        // write result - otherwise "en" is no longer excluded and LocalizedName takes a second English title.
        var series = new SeriesBuilder("Bleach")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithLocalizedNameAllowEmpty(string.Empty)
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        series.NameLocked = true;
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableName = true;
        metadataSettings.EnableLocalizedName = true;
        metadataSettings.GlobalNameLanguages = "en";
        metadataSettings.GlobalLocalizedNameLanguages = "en;ja-Latn";
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = "Bleach",
            LocalizedTitles = new Dictionary<string, IList<LocalizedTitleDto>>
            {
                // The second English title is what a self-check alone would wrongly accept
                ["en"] =
                [
                    new LocalizedTitleDto { Title = "Bleach" },
                    new LocalizedTitleDto { Title = "Bleach: Official" }
                ],
                ["ja-Latn"] = [new LocalizedTitleDto { Title = "Burichi" }]
            }
        }, 1);

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal("Bleach", postSeries.Name);
        Assert.Equal("Burichi", postSeries.LocalizedName);
    }

    [Fact]
    public async Task Name_NativeToken_ResolvesFromNativeTitle()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        var series = new SeriesBuilder("Original Name")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithLocalizedNameAllowEmpty(string.Empty)
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableName = true;
        // {Native} is not a language tag - it resolves to the provider's native title regardless of its language
        metadataSettings.GlobalNameLanguages = "{Native}";
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = "Bleach",
            Titles = new ALMediaTitle { NativeTitle = "Native Bleach" },
            LocalizedTitles = new Dictionary<string, IList<LocalizedTitleDto>>
            {
                ["en"] = [new LocalizedTitleDto { Title = "Bleach" }]
            }
        }, 1);

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal("Native Bleach", postSeries.Name);
    }

    [Fact]
    public async Task Name_NativeToken_ResolvesWithNoLocalizedTitles()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        var series = new SeriesBuilder("Original Name")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithLocalizedNameAllowEmpty(string.Empty)
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableName = true;
        metadataSettings.GlobalNameLanguages = "{Native}";
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        // A provider can send a native title with no per-language breakdown at all - the token must still resolve
        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = "Bleach",
            Titles = new ALMediaTitle { NativeTitle = "Native Bleach" }
        }, 1);

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal("Native Bleach", postSeries.Name);
    }

    [Fact]
    public async Task LocalizedName_NativeTokenSharedWithName_FallsToNextLanguage()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        var series = new SeriesBuilder("Original Name")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithLocalizedNameAllowEmpty(string.Empty)
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableName = true;
        metadataSettings.EnableLocalizedName = true;
        // Both lists lead with {Native}. Name eats it, so LocalizedName has to fall through to ja-Latn.
        metadataSettings.GlobalNameLanguages = "{Native}";
        metadataSettings.GlobalLocalizedNameLanguages = "{Native};ja-Latn";
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = "Bleach",
            Titles = new ALMediaTitle { NativeTitle = "Native Bleach" },
            LocalizedTitles = new Dictionary<string, IList<LocalizedTitleDto>>
            {
                ["ja-Latn"] = [new LocalizedTitleDto { Title = "Burichi" }]
            }
        }, 1);

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal("Native Bleach", postSeries.Name);
        Assert.Equal("Burichi", postSeries.LocalizedName);
    }

    [Fact]
    public async Task Name_NativeToken_EmptyNativeTitle_FallsToNextLanguage()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        var series = new SeriesBuilder("Original Name")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithLocalizedNameAllowEmpty(string.Empty)
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableName = true;
        metadataSettings.GlobalNameLanguages = "{Native};en";
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        // Provider sent no native title - the token is skipped and the next priority (en) wins
        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = "Bleach",
            Titles = new ALMediaTitle { NativeTitle = string.Empty },
            LocalizedTitles = new Dictionary<string, IList<LocalizedTitleDto>>
            {
                ["en"] = [new LocalizedTitleDto { Title = "Bleach" }]
            }
        }, 1);

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal("Bleach", postSeries.Name);
    }

    [Fact]
    public async Task Name_NativeToken_MatchesCaseInsensitively()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        var series = new SeriesBuilder("Original Name")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithLocalizedNameAllowEmpty(string.Empty)
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableName = true;
        // Admin-typed lowercase, versus the canonical {Native} casing
        metadataSettings.GlobalNameLanguages = "{native}";
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = "Bleach",
            Titles = new ALMediaTitle { NativeTitle = "Native Bleach" }
        }, 1);

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal("Native Bleach", postSeries.Name);
    }

    [Fact]
    public async Task Name_RomajiToken_ResolvesFromRomajiTitle()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        var series = new SeriesBuilder("Original Name")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithLocalizedNameAllowEmpty(string.Empty)
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableName = true;
        metadataSettings.GlobalNameLanguages = "{Romaji}";
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = "Bleach",
            Titles = new ALMediaTitle { RomajiTitle = "Burichi", NativeTitle = "Native Bleach" }
        }, 1);

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal("Burichi", postSeries.Name);
    }

    [Fact]
    public async Task NameAndLocalized_NativeAndRomajiTokens_ResolveIndependently()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        var series = new SeriesBuilder("Original Name")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithLocalizedNameAllowEmpty(string.Empty)
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableName = true;
        metadataSettings.EnableLocalizedName = true;
        // The two tokens are distinct: Name takes {Native}, LocalizedName takes {Romaji} - neither collides
        metadataSettings.GlobalNameLanguages = "{Native}";
        metadataSettings.GlobalLocalizedNameLanguages = "{Romaji}";
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = "Bleach",
            Titles = new ALMediaTitle { NativeTitle = "Native Bleach", RomajiTitle = "Burichi" }
        }, 1);

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal("Native Bleach", postSeries.Name);
        Assert.Equal("Burichi", postSeries.LocalizedName);
    }

    [Fact]
    public async Task LocalizedName_Override_MergedFolderAnchor_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        // A folder literally named "Chained Soldier" is merged under this series via LocalizedName.
        // OriginalName only anchors "Mato Seihei no Slave", so overwriting LocalizedName would orphan the folder.
        const string seriesName = "Mato Seihei no Slave";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithLocalizedName("Chained Soldier", true)
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1")
                    .WithFile(new MangaFileBuilder("C:/Manga/Chained Soldier/Chained Soldier v01.cbz", MangaFormat.Archive).Build())
                    .Build())
                .Build())
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableLocalizedName = true;
        metadataSettings.Overrides = [MetadataSettingField.LocalizedName];
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            LocalizedTitles = new Dictionary<string, IList<LocalizedTitleDto>>
            {
                ["ja-Latn"] = [new LocalizedTitleDto { Title = "Slave Corps" }]
            }
        }, 1);

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal("Chained Soldier", postSeries.LocalizedName);
    }

    [Fact]
    public async Task LocalizedName_Override_AliasNotOnDisk_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        // LocalizedName is a display alias no folder uses (files live under the series name), so overwriting
        // it is safe and the force override should still apply.
        const string seriesName = "Mato Seihei no Slave";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithLocalizedName("Localized Name here", true)
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1")
                    .WithFile(new MangaFileBuilder("C:/Manga/Mato Seihei no Slave/vol01.cbz", MangaFormat.Archive).Build())
                    .Build())
                .Build())
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableLocalizedName = true;
        metadataSettings.Overrides = [MetadataSettingField.LocalizedName];
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            LocalizedTitles = new Dictionary<string, IList<LocalizedTitleDto>>
            {
                ["ja-Latn"] = [new LocalizedTitleDto { Title = "Slave Corps" }]
            }
        }, 1);

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal("Slave Corps", postSeries.LocalizedName);
    }

    [Fact]
    public async Task Name_MergedFolderAnchor_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        // Name anchors a folder literally named "Mato Seihei no Slave"; OriginalName anchors the other folder
        // ("Chained Soldier"). Renaming Name to the external title would orphan the "Mato Seihei no Slave" folder.
        const string seriesName = "Mato Seihei no Slave";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithLocalizedNameAllowEmpty(string.Empty)
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1")
                    .WithFile(new MangaFileBuilder("C:/Manga/Mato Seihei no Slave/vol01.cbz", MangaFormat.Archive).Build())
                    .Build())
                .Build())
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        series.OriginalName = "Chained Soldier";
        series.NormalizedOriginalName = "Chained Soldier".ToNormalized();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableName = true;
        metadataSettings.EnableLocalizedName = false;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = "Some New Title",
            LocalizedTitles = new Dictionary<string, IList<LocalizedTitleDto>>
            {
                ["en"] = [new LocalizedTitleDto { Title = "Some New Title" }]
            }
        }, 1);

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(seriesName, postSeries.Name);
    }

    #endregion

    #region OrphanMergedFiles Validation Hooks

    // These cover the public wrappers the SeriesController calls when a user renames a series by hand. They mirror
    // the K+ guard but are the surface the manual-edit path relies on, so they get their own coverage.

    [Fact]
    public async Task WouldLocalizedNameChangeOrphanMergedFiles_DroppingMergedAnchor_ReturnsTrue()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        // A folder literally named "Chained Soldier" is merged under this series via LocalizedName. Name and
        // OriginalName only anchor "Mato Seihei no Slave", so changing LocalizedName would orphan the folder.
        var series = new SeriesBuilder("Mato Seihei no Slave")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithLocalizedName("Chained Soldier")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1")
                    .WithFile(new MangaFileBuilder("C:/Manga/Chained Soldier/Chained Soldier v01.cbz", MangaFormat.Archive).Build())
                    .Build())
                .Build())
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);

        Assert.True(await externalMetadataService.WouldLocalizedNameChangeOrphanMergedFiles(postSeries, "Slave Corps"));
    }

    [Fact]
    public async Task WouldLocalizedNameChangeOrphanMergedFiles_AliasNotOnDisk_ReturnsFalse()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        // LocalizedName is a display alias no folder uses (files live under the series name), so changing it is safe.
        var series = new SeriesBuilder("Mato Seihei no Slave")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithLocalizedName("Localized Name here")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1")
                    .WithFile(new MangaFileBuilder("C:/Manga/Mato Seihei no Slave/vol01.cbz", MangaFormat.Archive).Build())
                    .Build())
                .Build())
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);

        Assert.False(await externalMetadataService.WouldLocalizedNameChangeOrphanMergedFiles(postSeries, "Slave Corps"));
    }

    [Fact]
    public async Task WouldLocalizedNameChangeOrphanMergedFiles_Unchanged_ReturnsFalse()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        // Even though the localized name anchors a folder, the value isn't actually changing, so it's not orphaned.
        var series = new SeriesBuilder("Mato Seihei no Slave")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithLocalizedName("Chained Soldier")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1")
                    .WithFile(new MangaFileBuilder("C:/Manga/Chained Soldier/Chained Soldier v01.cbz", MangaFormat.Archive).Build())
                    .Build())
                .Build())
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);

        Assert.False(await externalMetadataService.WouldLocalizedNameChangeOrphanMergedFiles(postSeries, "Chained Soldier"));
    }

    [Fact]
    public async Task WouldLocalizedNameChangeOrphanMergedFiles_ClearingMergedAnchor_ReturnsTrue()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        // Clearing the field is still a drop - the "Chained Soldier" folder would be orphaned.
        var series = new SeriesBuilder("Mato Seihei no Slave")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithLocalizedName("Chained Soldier")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1")
                    .WithFile(new MangaFileBuilder("C:/Manga/Chained Soldier/Chained Soldier v01.cbz", MangaFormat.Archive).Build())
                    .Build())
                .Build())
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);

        Assert.True(await externalMetadataService.WouldLocalizedNameChangeOrphanMergedFiles(postSeries, null));
    }

    [Fact]
    public async Task WouldNameChangeOrphanMergedFiles_DroppingMergedAnchor_ReturnsTrue()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        // Name anchors the "Mato Seihei no Slave" folder; OriginalName anchors the other. Renaming Name would
        // orphan the folder held only by Name.
        var series = new SeriesBuilder("Mato Seihei no Slave")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithLocalizedNameAllowEmpty(string.Empty)
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1")
                    .WithFile(new MangaFileBuilder("C:/Manga/Mato Seihei no Slave/vol01.cbz", MangaFormat.Archive).Build())
                    .Build())
                .Build())
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        series.OriginalName = "Chained Soldier";
        series.NormalizedOriginalName = "Chained Soldier".ToNormalized();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);

        Assert.True(await externalMetadataService.WouldNameChangeOrphanMergedFiles(postSeries, "Some New Title"));
    }

    [Fact]
    public async Task WouldNameChangeOrphanMergedFiles_SafeRename_ReturnsFalse()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        // Files live under the LocalizedName folder, so the Name is only a display value - renaming it is safe.
        var series = new SeriesBuilder("Mato Seihei no Slave")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithLocalizedName("Chained Soldier")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1")
                    .WithFile(new MangaFileBuilder("C:/Manga/Chained Soldier/Chained Soldier v01.cbz", MangaFormat.Archive).Build())
                    .Build())
                .Build())
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);

        Assert.False(await externalMetadataService.WouldNameChangeOrphanMergedFiles(postSeries, "Some New Title"));
    }

    #endregion

    #region Publication Status

    [Fact]
    public async Task PublicationStatus_NoExisting_Off_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Publication Status";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePublicationStatus = false;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Volumes = 2
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(PublicationStatus.OnGoing, postSeries.Metadata.PublicationStatus);
    }

    [Fact]
    public async Task PublicationStatus_NoExisting_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Publication Status";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePublicationStatus = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Volumes = 2
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(PublicationStatus.Completed, postSeries.Metadata.PublicationStatus);
    }

    [Fact]
    public async Task PublicationStatus_Existing_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Publication Status";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithPublicationStatus(PublicationStatus.Hiatus)
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePublicationStatus = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Volumes = 2
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(PublicationStatus.Completed, postSeries.Metadata.PublicationStatus);
    }

    [Fact]
    public async Task PublicationStatus_Existing_Locked_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Publication Status";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithPublicationStatus(PublicationStatus.Hiatus, true)
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePublicationStatus = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Volumes = 2
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(PublicationStatus.Hiatus, postSeries.Metadata.PublicationStatus);
    }

    [Fact]
    public async Task PublicationStatus_Existing_Locked_Override_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Publication Status";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithPublicationStatus(PublicationStatus.Hiatus, true)
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePublicationStatus = true;
        metadataSettings.Overrides = [MetadataSettingField.PublicationStatus];
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Volumes = 2
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(PublicationStatus.Completed, postSeries.Metadata.PublicationStatus);
    }

    [Fact]
    public async Task PublicationStatus_Existing_CorrectState_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Publication Status";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithPublicationStatus(PublicationStatus.Hiatus)
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePublicationStatus = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Volumes = 2
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(PublicationStatus.Ended, postSeries.Metadata.PublicationStatus);
    }


    [Fact]
    public void IsSeriesCompleted_ExactMatch()
    {
        const string seriesName = "Test - Exact Match";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithMaxCount(5)
                .WithTotalCount(5)
                .Build())
            .Build();

        var chapters = new List<Chapter>();
        var externalMetadata = new ExternalSeriesDetailDto { Chapters = 5, Volumes = 0 };

        var result = ExternalMetadataService.IsSeriesCompleted(series, chapters, externalMetadata, Parser.DefaultChapterNumber);

        Assert.True(result);
    }

    [Fact]
    public void IsSeriesCompleted_Volumes_DecimalVolumes()
    {
        const string seriesName = "Test - Volume Complete";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithMaxCount(2)
                .WithTotalCount(3)
                .Build())
            .WithVolume(new VolumeBuilder("1").WithNumber(1).Build())
            .WithVolume(new VolumeBuilder("2").WithNumber(2).Build())
            .WithVolume(new VolumeBuilder("2.5").WithNumber(2.5f).Build())
            .Build();

        var chapters = new List<Chapter>();
        // External metadata includes decimal volume 2.5
        var externalMetadata = new ExternalSeriesDetailDto { Chapters = 0, Volumes = 3 };

        var result = ExternalMetadataService.IsSeriesCompleted(series, chapters, externalMetadata, 2);

        Assert.True(result);
        Assert.Equal(3, series.Metadata.MaxCount);
        Assert.Equal(3, series.Metadata.TotalCount);
    }

    /// <summary>
    /// This is validating that we get a completed even though we have a special chapter and AL doesn't count it
    /// </summary>
    [Fact]
    public void IsSeriesCompleted_Volumes_HasSpecialAndDecimal_ExternalNoSpecial()
    {
        const string seriesName = "Test - Volume Complete";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithMaxCount(2)
                .WithTotalCount(3)
                .Build())
            .WithVolume(new VolumeBuilder("1").WithNumber(1).Build())
            .WithVolume(new VolumeBuilder("1.5").WithNumber(1.5f).Build())
            .WithVolume(new VolumeBuilder("2").WithNumber(2).Build())
            .WithVolume(new VolumeBuilder(Parser.SpecialVolume).Build())
            .Build();

        var chapters = new List<Chapter>();
        // External metadata includes volume 1.5, but not the special
        var externalMetadata = new ExternalSeriesDetailDto { Chapters = 0, Volumes = 3 };

        var result = ExternalMetadataService.IsSeriesCompleted(series, chapters, externalMetadata, 2);

        Assert.True(result);
        Assert.Equal(3, series.Metadata.MaxCount);
        Assert.Equal(3, series.Metadata.TotalCount);
    }

    /// <remarks>
    /// This unit test also illustrates the bug where you may get a false positive if you had Volumes 1,2, and 2.1. While
    /// missing volume 3. With the external metadata expecting non-decimal volumes.
    /// i.e. it would fail if we only had one decimal volume
    /// </remarks>
    [Fact]
    public void IsSeriesCompleted_Volumes_TooManyDecimalVolumes()
    {
        const string seriesName = "Test - Volume Complete";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithMaxCount(2)
                .WithTotalCount(3)
                .Build())
            .WithVolume(new VolumeBuilder("1").WithNumber(1).Build())
            .WithVolume(new VolumeBuilder("2").WithNumber(2).Build())
            .WithVolume(new VolumeBuilder("2.1").WithNumber(2.1f).Build())
            .WithVolume(new VolumeBuilder("2.2").WithNumber(2.2f).Build())
            .Build();

        var chapters = new List<Chapter>();
        // External metadata includes no special or decimals. There are 3 volumes. And we're missing volume 3
        var externalMetadata = new ExternalSeriesDetailDto { Chapters = 0, Volumes = 3 };

        var result = ExternalMetadataService.IsSeriesCompleted(series, chapters, externalMetadata, 2);

        Assert.False(result);
    }

    [Fact]
    public void IsSeriesCompleted_NoVolumes_GEQChapterCheck()
    {
        // We own 11 chapters, the external metadata expects 10
        const string seriesName = "Test - Chapter MaxCount, no volumes";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithMaxCount(11)
                .WithTotalCount(10)
                .Build())
            .Build();

        var chapters = new List<Chapter>();
        var externalMetadata = new ExternalSeriesDetailDto { Chapters = 10, Volumes = 0 };

        var result = ExternalMetadataService.IsSeriesCompleted(series, chapters, externalMetadata, Parser.DefaultChapterNumber);

        Assert.True(result);
        Assert.Equal(11, series.Metadata.TotalCount);
        Assert.Equal(11, series.Metadata.MaxCount);
    }

    [Fact]
    public void IsSeriesCompleted_NoVolumes_IncludeAllChaptersCheck()
    {
        const string seriesName = "Test - Chapter Count";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithMaxCount(7)
                .WithTotalCount(10)
                .Build())
            .Build();

        var chapters = new List<Chapter>
        {
            new ChapterBuilder("0").Build(),
            new ChapterBuilder("2").Build(),
            new ChapterBuilder("3").Build(),
            new ChapterBuilder("4").Build(),
            new ChapterBuilder("5").Build(),
            new ChapterBuilder("6").Build(),
            new ChapterBuilder("7").Build(),
            new ChapterBuilder("7.1").Build(),
            new ChapterBuilder("7.2").Build(),
            new ChapterBuilder("7.3").Build()
        };
        // External metadata includes prologues (0) and extra's (7.X)
        var externalMetadata = new ExternalSeriesDetailDto { Chapters = 10, Volumes = 0 };

        var result = ExternalMetadataService.IsSeriesCompleted(series, chapters, externalMetadata, Parser.DefaultChapterNumber);

        Assert.True(result);
        Assert.Equal(10, series.Metadata.TotalCount);
        Assert.Equal(10, series.Metadata.MaxCount);
    }

    [Fact]
    public void IsSeriesCompleted_NotEnoughVolumes()
    {
        const string seriesName = "Test - Incomplete Volume";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithMaxCount(2)
                .WithTotalCount(5)
                .Build())
            .WithVolume(new VolumeBuilder("1").WithNumber(1).Build())
            .WithVolume(new VolumeBuilder("2").WithNumber(2).Build())
            .Build();

        var chapters = new List<Chapter>();
        var externalMetadata = new ExternalSeriesDetailDto { Chapters = 0, Volumes = 5 };

        var result = ExternalMetadataService.IsSeriesCompleted(series, chapters, externalMetadata, 2);

        Assert.False(result);
    }

    [Fact]
    public void IsSeriesCompleted_NoVolumes_NotEnoughChapters()
    {
        const string seriesName = "Test - Incomplete Chapter";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithMaxCount(5)
                .WithTotalCount(8)
                .Build())
            .Build();

        var chapters = new List<Chapter>
        {
            new ChapterBuilder("1").Build(),
            new ChapterBuilder("2").Build(),
            new ChapterBuilder("3").Build()
        };
        var externalMetadata = new ExternalSeriesDetailDto { Chapters = 10, Volumes = 0 };

        var result = ExternalMetadataService.IsSeriesCompleted(series, chapters, externalMetadata, Parser.DefaultChapterNumber);

        Assert.False(result);
    }


    #endregion

    #region Publication Status - Count Selection

    private static Series BuildPublicationStatusTestSeries(string seriesName, int volumesOnDisk, int looseChaptersOnDisk)
    {
        var builder = new SeriesBuilder(seriesName).WithLibraryId(1);

        if (volumesOnDisk > 0)
        {
            for (var i = 1; i <= volumesOnDisk; i++)
            {
                builder = builder.WithVolume(new VolumeBuilder(i.ToString())
                    .WithNumber(i)
                    .WithChapter(new ChapterBuilder(i.ToString()).Build())
                    .Build());
            }
        }

        if (looseChaptersOnDisk > 0)
        {
            var looseVolume = new VolumeBuilder(Parser.LooseLeafVolume);

            for (var i = 1; i <= looseChaptersOnDisk; i++)
            {
                looseVolume = looseVolume.WithChapter(new ChapterBuilder(i.ToString()).Build());
            }

            builder = builder.WithVolume(looseVolume.Build());
        }

        return builder.WithMetadata(new SeriesMetadataBuilder().Build()).Build();
    }

    [Theory]
    // Only loose chapters: use chapters
    [InlineData(0, 24, 0, 36, 36, 24, PublicationStatus.Ended)]
    [InlineData(0, 36, 0, 36, 36, 36, PublicationStatus.Completed)]
    // Only volumes: use Volumes
    [InlineData(3, 0, 3, 0, 3, 3, PublicationStatus.Completed)]
    [InlineData(3, 0, 7, 0, 7, 3, PublicationStatus.Ended)]
    // Volumes + some loose/unassigned chapters on disk: use volumes
    [InlineData(3, 5, 7, 36, 7, 3, PublicationStatus.Ended)]
    // Volume-based series, but no volumes from K+. Don't use (Not valid count)
    [InlineData(3, 0, 0, 36, 0, 3, PublicationStatus.OnGoing)]
    public async Task DeterminePublicationStatus_UsesCorrectCount(
        int volumesOnDisk,
        int looseChaptersOnDisk,
        int externalVolumes,
        int externalChapters,
        int expectedTotalCount,
        int expectedMaxCount,
        PublicationStatus expectedStatus)
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        var seriesName = $"Test - DeterminePublicationStatus_UsesCorrectCount {volumesOnDisk}v-{looseChaptersOnDisk}c";
        var series = BuildPublicationStatusTestSeries(seriesName, volumesOnDisk, looseChaptersOnDisk);
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePublicationStatus = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto
        {
            Name = seriesName,
            Volumes = externalVolumes,
            Chapters = externalChapters
        }, series.Id);

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(series.Id, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(expectedTotalCount, postSeries.Metadata.TotalCount);
        Assert.Equal(expectedMaxCount, postSeries.Metadata.MaxCount);
        Assert.Equal(expectedStatus, postSeries.Metadata.PublicationStatus);
    }

    #endregion

    #region Age Rating

    [Fact]
    public async Task AgeRating_NoExisting_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Age Rating";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.AgeRatingMappings = new Dictionary<string, AgeRating>()
        {
            {"Ecchi", AgeRating.Teen}, // Genre
            {"H", AgeRating.R18Plus}, // Tag
        };
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(AgeRating.Teen, postSeries.Metadata.AgeRating);
    }

    [Fact]
    public async Task AgeRating_ExistingHigher_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Age Rating";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithAgeRating(AgeRating.Mature)
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.AgeRatingMappings = new Dictionary<string, AgeRating>()
        {
            {"Ecchi", AgeRating.Teen}, // Genre
            {"H", AgeRating.R18Plus}, // Tag
        };
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(AgeRating.Mature, postSeries.Metadata.AgeRating);
    }

    [Fact]
    public async Task AgeRating_ExistingLower_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Age Rating";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithAgeRating(AgeRating.Everyone)
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.AgeRatingMappings = new Dictionary<string, AgeRating>()
        {
            {"Ecchi", AgeRating.Teen}, // Genre
            {"H", AgeRating.R18Plus}, // Tag
        };
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(AgeRating.Teen, postSeries.Metadata.AgeRating);
    }

    [Fact]
    public async Task AgeRating_Existing_Locked_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Age Rating";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithAgeRating(AgeRating.Everyone, true)
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.AgeRatingMappings = new Dictionary<string, AgeRating>()
        {
            {"Ecchi", AgeRating.Teen}, // Genre
            {"H", AgeRating.R18Plus}, // Tag
        };
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(AgeRating.Everyone, postSeries.Metadata.AgeRating);
    }

    [Fact]
    public async Task AgeRating_Existing_Locked_Override_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Age Rating";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithAgeRating(AgeRating.Everyone, true)
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.Overrides = [MetadataSettingField.AgeRating];
        metadataSettings.AgeRatingMappings = new Dictionary<string, AgeRating>()
        {
            {"Ecchi", AgeRating.Teen}, // Genre
            {"H", AgeRating.R18Plus}, // Tag
        };
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(AgeRating.Teen, postSeries.Metadata.AgeRating);
    }

    [Fact]
    public void AgeRating_NormalizedMapping()
    {
        var tags = new List<string> { "tAg$'1", "tag2" };
        var mappings = new Dictionary<string, AgeRating>()
        {
            ["tag1"] = AgeRating.Teen,
        };

        Assert.Equal(AgeRating.Teen, ExternalMetadataService.DetermineAgeRating(tags, mappings));

        mappings.Add("tag2", AgeRating.AdultsOnly);
        Assert.Equal(AgeRating.AdultsOnly, ExternalMetadataService.DetermineAgeRating(tags, mappings));
    }

    [Fact]
    public async Task AgeRating_Raw_WithMapping_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Age Rating Raw";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.ExternalAgeRatingMappings = new Dictionary<string, AgeRating>()
        {
            {"Mature 17+", AgeRating.Mature17Plus},
        };
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            AgeRatingRaw = "Mature 17+"
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(AgeRating.Mature17Plus, postSeries.Metadata.AgeRating);
    }

    /// <summary>
    /// The raw string is run through <see cref="ExternalMetadataService.DetermineAgeRating"/>, so the mapping key
    /// matches on the normalized form rather than an exact string match
    /// </summary>
    [Fact]
    public async Task AgeRating_Raw_MappingIsNormalized_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Age Rating Raw";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.ExternalAgeRatingMappings = new Dictionary<string, AgeRating>()
        {
            {"mature 17+", AgeRating.Mature17Plus},
        };
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            AgeRatingRaw = "MATURE 17 +"
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(AgeRating.Mature17Plus, postSeries.Metadata.AgeRating);
    }

    [Fact]
    public async Task AgeRating_Raw_NoMapping_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Age Rating Raw";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.ExternalAgeRatingMappings = new Dictionary<string, AgeRating>()
        {
            {"Mature 17+", AgeRating.Mature17Plus},
        };
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            AgeRatingRaw = "Unrated"
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(AgeRating.Unknown, postSeries.Metadata.AgeRating);
        Assert.DoesNotContain(MetadataSettingField.AgeRating, postSeries.Metadata.KPlusOverrides);
    }

    /// <summary>
    /// Kavita+ maps the content rating itself and sends it down on AgeRating, which is a candidate on its own
    /// </summary>
    [Fact]
    public async Task AgeRating_ExternalAgeRating_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Age Rating Raw";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            AgeRating = AgeRating.Teen
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(AgeRating.Teen, postSeries.Metadata.AgeRating);
        Assert.Contains(MetadataSettingField.AgeRating, postSeries.Metadata.KPlusOverrides);
    }

    /// <summary>
    /// The highest of (existing, Kavita+ AgeRating, raw string mapping, tag/genre mapping) wins
    /// </summary>
    [Fact]
    public async Task AgeRating_HighestOfAllCandidates_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Age Rating Raw";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithAgeRating(AgeRating.Everyone)
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.AgeRatingMappings = new Dictionary<string, AgeRating>()
        {
            {"Ecchi", AgeRating.G}, // Genre
        };
        metadataSettings.ExternalAgeRatingMappings = new Dictionary<string, AgeRating>()
        {
            {"Seinen", AgeRating.Mature},
        };
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"],       // -> G
            AgeRating = AgeRating.Teen,
            AgeRatingRaw = "Seinen"   // -> Mature
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(AgeRating.Mature, postSeries.Metadata.AgeRating);
    }

    [Fact]
    public async Task AgeRating_ExistingHigherThanAllCandidates_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Age Rating Raw";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithAgeRating(AgeRating.X18Plus)
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.ExternalAgeRatingMappings = new Dictionary<string, AgeRating>()
        {
            {"Seinen", AgeRating.Mature},
        };
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            AgeRating = AgeRating.Teen,
            AgeRatingRaw = "Seinen"
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(AgeRating.X18Plus, postSeries.Metadata.AgeRating);
        Assert.DoesNotContain(MetadataSettingField.AgeRating, postSeries.Metadata.KPlusOverrides);
    }

    /// <summary>
    /// When the winning candidate is what the Series already has, nothing is written and no Kavita+ override is recorded
    /// </summary>
    [Fact]
    public async Task AgeRating_EqualToExisting_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Age Rating Raw";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithAgeRating(AgeRating.Teen)
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.AgeRatingMappings = new Dictionary<string, AgeRating>()
        {
            {"Ecchi", AgeRating.Teen}, // Genre
        };
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"],
            AgeRating = AgeRating.Teen
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(AgeRating.Teen, postSeries.Metadata.AgeRating);
        Assert.DoesNotContain(MetadataSettingField.AgeRating, postSeries.Metadata.KPlusOverrides);
    }

    [Fact]
    public async Task AgeRating_AllCandidatesUnknown_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Age Rating Raw";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(AgeRating.Unknown, postSeries.Metadata.AgeRating);
        Assert.DoesNotContain(MetadataSettingField.AgeRating, postSeries.Metadata.KPlusOverrides);
    }

    [Fact]
    public async Task AgeRating_Raw_Locked_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Age Rating Raw";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithAgeRating(AgeRating.Everyone, true)
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.ExternalAgeRatingMappings = new Dictionary<string, AgeRating>()
        {
            {"Seinen", AgeRating.Mature},
        };
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            AgeRatingRaw = "Seinen"
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(AgeRating.Everyone, postSeries.Metadata.AgeRating);
    }

    [Fact]
    public async Task AgeRating_Raw_Locked_Override_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Age Rating Raw";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithAgeRating(AgeRating.Everyone, true)
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.Overrides = [MetadataSettingField.AgeRating];
        metadataSettings.ExternalAgeRatingMappings = new Dictionary<string, AgeRating>()
        {
            {"Seinen", AgeRating.Mature},
        };
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            AgeRatingRaw = "Seinen"
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(AgeRating.Mature, postSeries.Metadata.AgeRating);
    }

    #endregion

    #region Genres

    [Fact]
    public async Task Genres_NoExisting_Off_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Genres";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableGenres = false;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal([], postSeries.Metadata.Genres);
    }

    [Fact]
    public async Task Genres_NoExisting_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Genres";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableGenres = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["Ecchi"], postSeries.Metadata.Genres.Select(g => g.Title));
    }

    [Fact]
    public async Task Genres_Existing_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, genreLookup, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Genres";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithGenre(genreLookup["Action"])
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableGenres = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["Ecchi"], postSeries.Metadata.Genres.Select(g => g.Title));
    }

    [Fact]
    public async Task Genres_Existing_Locked_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, genreLookup, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Genres";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithGenre(genreLookup["Action"], true)
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableGenres = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["Action"], postSeries.Metadata.Genres.Select(g => g.Title));
    }

    [Fact]
    public async Task Genres_Existing_Locked_Override_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, genreLookup, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Genres";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithGenre(genreLookup["Action"], true)
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableGenres = true;
        metadataSettings.Overrides = [MetadataSettingField.Genres];
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["Ecchi"], postSeries.Metadata.Genres.Select(g => g.Title));
    }

    #endregion

    #region Tags

    [Fact]
    public async Task Tags_NoExisting_Off_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Tags";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = false;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags = [new MetadataTagDto() {Name = "Boxing"}]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal([], postSeries.Metadata.Tags);
    }

    [Fact]
    public async Task Tags_NoExisting_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Tags";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags = [new MetadataTagDto() {Name = "Boxing"}]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["Boxing"], postSeries.Metadata.Tags.Select(t => t.Title));
    }

    [Fact]
    public async Task Tags_Existing_Locked_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, tagLookup, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Tags";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithTag(tagLookup["H"], true)
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags = [new MetadataTagDto() {Name = "Boxing"}]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["H"], postSeries.Metadata.Tags.Select(t => t.Title));
    }

    [Fact]
    public async Task Tags_Existing_Locked_Override_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, tagLookup, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Tags";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithTag(tagLookup["H"], true)
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = true;
        metadataSettings.Overrides = [MetadataSettingField.Tags];
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags = [new MetadataTagDto() {Name = "Boxing"}]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["Boxing"], postSeries.Metadata.Tags.Select(t => t.Title));
    }

    #endregion

    #region Tag Weight Filtering

    [Fact]
    public async Task TagWeight_NoFilterSet_AllTagsKept()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Tag Weight";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = true;
        metadataSettings.FilterAboveWeight = null;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags =
            [
                new MetadataTagDto() {Name = "H", TagWeight = TagWeight.Core},
                new MetadataTagDto() {Name = "Boxing", TagWeight = TagWeight.Unweighted},
            ]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        var tags = postSeries.Metadata.Tags.Select(t => t.Title).ToList();
        Assert.Contains("H", tags);
        Assert.Contains("Boxing", tags);
    }

    /// <summary>
    /// The filter is exclusive, tags at the configured weight survive, only those above (lesser impact) are removed
    /// </summary>
    [Fact]
    public async Task TagWeight_FilterAboveRecurrent_KeepsBoundaryRemovesLesser()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Tag Weight";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = true;
        metadataSettings.FilterAboveWeight = TagWeight.Recurrent;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags =
            [
                new MetadataTagDto() {Name = "Boxing", TagWeight = TagWeight.Core},
                new MetadataTagDto() {Name = "Fighting", TagWeight = TagWeight.Defining},
                new MetadataTagDto() {Name = "Sports", TagWeight = TagWeight.Recurrent},
                new MetadataTagDto() {Name = "Slice of Life", TagWeight = TagWeight.Incidental},
                new MetadataTagDto() {Name = "Drama", TagWeight = TagWeight.Unweighted},
            ]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        var tags = postSeries.Metadata.Tags.Select(t => t.Title).ToList();
        Assert.Contains("Boxing", tags);
        Assert.Contains("Fighting", tags);
        Assert.Contains("Sports", tags);
        Assert.DoesNotContain("Slice of Life", tags);
        Assert.DoesNotContain("Drama", tags);
    }

    /// <summary>
    /// A null weight means the provider doesn't support weights, those tags must never be filtered out
    /// </summary>
    [Fact]
    public async Task TagWeight_NullWeight_Kept()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Tag Weight";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = true;
        metadataSettings.FilterAboveWeight = TagWeight.Core;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags = [new MetadataTagDto() {Name = "Boxing"}]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["Boxing"], postSeries.Metadata.Tags.Select(t => t.Title));
    }

    /// <summary>
    /// Control for <see cref="TagWeight_Whitelisted_AboveWeightTagKept"/>, same tags with no whitelist
    /// </summary>
    [Fact]
    public async Task TagWeight_NotWhitelisted_AboveWeightTagRemoved()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Tag Weight";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = true;
        metadataSettings.Whitelist = [];
        metadataSettings.FilterAboveWeight = TagWeight.Recurrent;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags =
            [
                new MetadataTagDto() {Name = "H", TagWeight = TagWeight.Core},
                new MetadataTagDto() {Name = "Boxing", TagWeight = TagWeight.Unweighted},
            ]
        }, 1);

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["H"], postSeries.Metadata.Tags.Select(t => t.Title));
    }

    /// <summary>
    /// A whitelisted tag is exempt from weight filtering. Note the whitelist is an allow-list for tags, so every
    /// expected survivor has to be on it for this to isolate the weight logic
    /// </summary>
    [Fact]
    public async Task TagWeight_Whitelisted_AboveWeightTagKept()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Tag Weight";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = true;
        metadataSettings.Whitelist = ["H", "Boxing"];
        metadataSettings.FilterAboveWeight = TagWeight.Recurrent;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags =
            [
                new MetadataTagDto() {Name = "H", TagWeight = TagWeight.Core},
                new MetadataTagDto() {Name = "Boxing", TagWeight = TagWeight.Unweighted},
            ]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        var tags = postSeries.Metadata.Tags.Select(t => t.Title).ToList();
        Assert.Contains("H", tags);
        Assert.Contains("Boxing", tags);
    }

    /// <summary>
    /// The whitelist match is case-insensitive
    /// </summary>
    [Fact]
    public async Task TagWeight_WhitelistedDifferentCasing_AboveWeightTagKept()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Tag Weight";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = true;
        metadataSettings.Whitelist = ["boxing"];
        metadataSettings.FilterAboveWeight = TagWeight.Recurrent;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags = [new MetadataTagDto() {Name = "Boxing", TagWeight = TagWeight.Unweighted}]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["Boxing"], postSeries.Metadata.Tags.Select(t => t.Title));
    }

    /// <summary>
    /// Filtering runs on the source tag names after mapping, so a mapped destination name survives even when the
    /// source tag it came from is above the weight
    /// </summary>
    [Fact]
    public async Task TagWeight_MappedTagName_SurvivesFilter()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Tag Weight";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = true;
        metadataSettings.FilterAboveWeight = TagWeight.Recurrent;
        metadataSettings.FieldMappings = [new MetadataFieldMapping()
        {
            SourceType = MetadataFieldType.Tag,
            SourceValue = "Boxing",
            DestinationType = MetadataFieldType.Tag,
            DestinationValue = "Sports",
            ExcludeFromSource = false
        }];
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags = [new MetadataTagDto() {Name = "Boxing", TagWeight = TagWeight.Unweighted}]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["Sports"], postSeries.Metadata.Tags.Select(t => t.Title));
    }

    /// <summary>
    /// Weight filtering only applies to tags, a tag mapped over to a Genre is unaffected
    /// </summary>
    [Fact]
    public async Task TagWeight_TagMappedToGenre_NotFiltered()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Tag Weight";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = true;
        metadataSettings.EnableGenres = true;
        metadataSettings.FilterAboveWeight = TagWeight.Recurrent;
        metadataSettings.FieldMappings = [new MetadataFieldMapping()
        {
            SourceType = MetadataFieldType.Tag,
            SourceValue = "Boxing",
            DestinationType = MetadataFieldType.Genre,
            DestinationValue = "Sports",
            ExcludeFromSource = true
        }];
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags = [new MetadataTagDto() {Name = "Boxing", TagWeight = TagWeight.Unweighted}]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["Sports"], postSeries.Metadata.Genres.Select(g => g.Title));
        Assert.Equal([], postSeries.Metadata.Tags);
    }

    /// <summary>
    /// Age Rating is derived from all external tags, before weight filtering, so a tag too weak to be written
    /// still raises the rating
    /// </summary>
    [Fact]
    public async Task TagWeight_FilteredTag_NotWrittenButStillRaisesAgeRating()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Tag Weight";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = true;
        metadataSettings.FilterAboveWeight = TagWeight.Recurrent;
        metadataSettings.AgeRatingMappings = new Dictionary<string, AgeRating>()
        {
            {"H", AgeRating.R18Plus},
        };
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags = [new MetadataTagDto() {Name = "H", TagWeight = TagWeight.Unweighted}]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal([], postSeries.Metadata.Tags);
        Assert.Equal(AgeRating.R18Plus, postSeries.Metadata.AgeRating);
    }

    /// <summary>
    /// Control for <see cref="TagWeight_FilteredTag_NotWrittenButStillRaisesAgeRating"/>. Weight decides whether the
    /// tag is written, it never decides the rating
    /// </summary>
    [Fact]
    public async Task TagWeight_UnfilteredTag_WrittenAndRaisesAgeRating()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Tag Weight";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = true;
        metadataSettings.FilterAboveWeight = TagWeight.Recurrent;
        metadataSettings.AgeRatingMappings = new Dictionary<string, AgeRating>()
        {
            {"H", AgeRating.R18Plus},
        };
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags = [new MetadataTagDto() {Name = "H", TagWeight = TagWeight.Core}]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["H"], postSeries.Metadata.Tags.Select(t => t.Title));
        Assert.Equal(AgeRating.R18Plus, postSeries.Metadata.AgeRating);
    }

    /// <summary>
    /// The blacklist guards what gets written, not what the rating is derived from, so a blacklisted tag still
    /// raises the Age Rating
    /// </summary>
    [Fact]
    public async Task TagWeight_BlacklistedTag_NotWrittenButStillRaisesAgeRating()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Tag Weight";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = true;
        metadataSettings.Blacklist = ["H"];
        metadataSettings.AgeRatingMappings = new Dictionary<string, AgeRating>()
        {
            {"H", AgeRating.R18Plus},
        };
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags = [new MetadataTagDto() {Name = "H", TagWeight = TagWeight.Core}]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal([], postSeries.Metadata.Tags);
        Assert.Equal(AgeRating.R18Plus, postSeries.Metadata.AgeRating);
    }

    #endregion

    #region FieldMappings

    [Fact]
    public void GenerateGenreAndTagLists_Normalized_Mappings()
    {
        var settings = new MetadataSettingsDto
        {
            EnableExtendedMetadataProcessing = true,
            Whitelist = [],
            Blacklist = [],
            FieldMappings = [
                new MetadataFieldMappingDto
                {
                    SourceType = MetadataFieldType.Tag,
                    SourceValue = "Girls love",
                    DestinationType = MetadataFieldType.Genre,
                    DestinationValue = "Yuri",
                    ExcludeFromSource = false,
                },
                new MetadataFieldMappingDto
                {
                    SourceType = MetadataFieldType.Tag,
                    SourceValue = "Girls love",
                    DestinationType = MetadataFieldType.Genre,
                    DestinationValue = "Romance",
                    ExcludeFromSource = false,
                },
                new MetadataFieldMappingDto
                {
                    SourceType = MetadataFieldType.Genre,
                    SourceValue = "WW2",
                    DestinationType = MetadataFieldType.Genre,
                    DestinationValue = "War",
                    ExcludeFromSource = true,
                },
            ],
        };

        var tags = new List<string> { "Girl's Love", "Unrelated tag" };
        var genres = new List<string> { "Ww2", "Unrelated genre" };

        ExternalMetadataService.GenerateExternalGenreAndTagsList(genres, tags, settings,
            out var finalTags, out var finalGenres);

        Assert.Contains("Unrelated tag", finalTags);

        Assert.Contains("Yuri", finalGenres);
        Assert.Contains("Romance", finalGenres);
        Assert.Contains("Unrelated genre", finalGenres);
        Assert.DoesNotContain("Ww2", finalGenres);
    }

    [Fact]
    public void GenerateGenreAndTagLists_RemoveIfAnyRemoves()
    {
        var settings = new MetadataSettingsDto
        {
            EnableExtendedMetadataProcessing = true,
            Whitelist = [],
            Blacklist = [],
            FieldMappings = [
                new MetadataFieldMappingDto
                {
                    SourceType = MetadataFieldType.Tag,
                    SourceValue = "Girls love",
                    DestinationType = MetadataFieldType.Genre,
                    DestinationValue = "Yuri",
                    ExcludeFromSource = false,
                },
                new MetadataFieldMappingDto
                {
                    SourceType = MetadataFieldType.Tag,
                    SourceValue = "Girls love",
                    DestinationType = MetadataFieldType.Genre,
                    DestinationValue = "Romance",
                    ExcludeFromSource = true,
                },
            ],
        };

        var tags = new List<string> { "Girl's Love"};
        var genres = new List<string>();

        ExternalMetadataService.GenerateExternalGenreAndTagsList(genres, tags, settings,
            out _, out var finalGenres);

        Assert.Contains("Yuri", finalGenres);
        Assert.Contains("Romance", finalGenres);
        Assert.DoesNotContain("Girls Love", finalGenres);
    }


    #endregion

    #region People - Writers/Artists

    [Fact]
    public async Task People_Writer_NoExisting_Off_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - People - Writer/Artists";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = false;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Staff = [CreateStaff("John", "Doe", "Story")]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal([], postSeries.Metadata.People.Where(p => p.Role == PersonRole.Writer));
    }

    [Fact]
    public async Task People_Writer_NoExisting_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - People - Writer/Artists";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = true;
        metadataSettings.FirstLastPeopleNaming = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Staff = [CreateStaff("John", "Doe", "Story")]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["John Doe"], postSeries.Metadata.People.Where(p => p.Role == PersonRole.Writer).Select(p => p.Person.Name));
    }

    [Fact]
    public async Task People_Writer_Locked_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, personLookup) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - People - Writer/Artists";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithPerson(personLookup["Johnny Twowheeler"], PersonRole.Writer)
                .Build())
            .Build();
        series.Metadata.WriterLocked = true;
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = true;
        metadataSettings.FirstLastPeopleNaming = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Staff = [CreateStaff("John", "Doe", "Story")]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[]{"Johnny Twowheeler"}.OrderBy(s => s),
            postSeries.Metadata.People.Where(p => p.Role == PersonRole.Writer)
                .Select(p => p.Person.Name)
                .OrderBy(s => s));
    }

    [Fact]
    public async Task People_Writer_Locked_Override_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, personLookup) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - People - Writer/Artists";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithPerson(personLookup["Johnny Twowheeler"], PersonRole.Writer)
                .Build())
            .Build();
        series.Metadata.WriterLocked = true;
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = true;
        metadataSettings.FirstLastPeopleNaming = true;
        metadataSettings.Overrides = [MetadataSettingField.People];
        metadataSettings.PersonRoles = [PersonRole.Writer];
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Staff = [CreateStaff("John", "Doe", "Story")]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[]{"John Doe", "Johnny Twowheeler"}.OrderBy(s => s),
            postSeries.Metadata.People.Where(p => p.Role == PersonRole.Writer)
                .Select(p => p.Person.Name)
                .OrderBy(s => s));
        Assert.True( postSeries.Metadata.People.Where(p => p.Role == PersonRole.Writer)
            .FirstOrDefault(p => p.Person.Name == "John Doe")!.KavitaPlusConnection);
    }

    [Fact]
    public async Task People_Writer_Locked_Override_ReverseNamingMatch_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, personLookup) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - People - Writer/Artists";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithPerson(personLookup["Johnny Twowheeler"], PersonRole.Writer)
                .Build())
            .Build();
        series.Metadata.WriterLocked = true;
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = true;
        metadataSettings.FirstLastPeopleNaming = false;
        metadataSettings.Overrides = [MetadataSettingField.People];
        metadataSettings.PersonRoles = [PersonRole.Writer];
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Staff = [CreateStaff("Twowheeler", "Johnny", "Story")]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[]{"Johnny Twowheeler"}.OrderBy(s => s),
            postSeries.Metadata.People.Where(p => p.Role == PersonRole.Writer)
                .Select(p => p.Person.Name)
                .OrderBy(s => s));
    }

    [Fact]
    public async Task People_Writer_Locked_Override_PersonRoleNotSet_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, personLookup) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - People - Writer/Artists";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithPerson(personLookup["Johnny Twowheeler"], PersonRole.Writer)
                .Build())
            .Build();
        series.Metadata.WriterLocked = true;
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = true;
        metadataSettings.FirstLastPeopleNaming = true;
        metadataSettings.Overrides = [MetadataSettingField.People];
        metadataSettings.PersonRoles = [];
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Staff = [CreateStaff("John", "Doe", "Story")]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[]{"Johnny Twowheeler"}.OrderBy(s => s),
            postSeries.Metadata.People.Where(p => p.Role == PersonRole.Writer)
                .Select(p => p.Person.Name)
                .OrderBy(s => s));
    }


    [Fact]
    public async Task People_Writer_OverrideReMatchDeletesOld_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - People - Writer/Artists";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = true;
        metadataSettings.FirstLastPeopleNaming = true;
        metadataSettings.Overrides = [MetadataSettingField.People];
        metadataSettings.PersonRoles = [PersonRole.Writer];
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Staff = [CreateStaff("John", "Doe", "Story")]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[]{"John Doe"}.OrderBy(s => s),
            postSeries.Metadata.People.Where(p => p.Role == PersonRole.Writer)
                .Select(p => p.Person.Name)
                .OrderBy(s => s));

        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Staff = [CreateStaff("John", "Doe 2", "Story")]
        }, 1);

        postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[]{"John Doe 2"}.OrderBy(s => s),
            postSeries.Metadata.People.Where(p => p.Role == PersonRole.Writer)
                .Select(p => p.Person.Name)
                .OrderBy(s => s));
    }

    #endregion

    #region People Alias

    [Fact]
    public async Task PeopleAliasing_AddAsAlias()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - People - Add as Alias";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        context.Person.Add(new PersonBuilder("John Doe").Build());
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = true;
        metadataSettings.FirstLastPeopleNaming = true;
        metadataSettings.Overrides = [MetadataSettingField.People];
        metadataSettings.PersonRoles = [PersonRole.Writer];
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Staff = [CreateStaff("Doe", "John", "Story")]
        }, 1);

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);

        var allWriters = postSeries.Metadata.People.Where(p => p.Role == PersonRole.Writer).ToList();
        Assert.Single(allWriters);

        var johnDoe = allWriters[0].Person;

        Assert.Contains("Doe John", johnDoe.Aliases.Select(pa => pa.Alias));
    }

    [Fact]
    public async Task PeopleAliasing_AddOnAlias()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - People - Add as Alias";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);

        context.Person.Add(new PersonBuilder("John Doe").WithAlias("Doe John").Build());

        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = true;
        metadataSettings.FirstLastPeopleNaming = true;
        metadataSettings.Overrides = [MetadataSettingField.People];
        metadataSettings.PersonRoles = [PersonRole.Writer];
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Staff = [CreateStaff("Doe", "John", "Story")]
        }, 1);

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);

        var allWriters = postSeries.Metadata.People.Where(p => p.Role == PersonRole.Writer).ToList();
        Assert.Single(allWriters);

        var johnDoe = allWriters[0].Person;

        Assert.Contains("Doe John", johnDoe.Aliases.Select(pa => pa.Alias));
    }

    [Fact]
    public async Task PeopleAliasing_DontAddAsAlias_SameButNotSwitched()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - People - Add as Alias";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = true;
        metadataSettings.FirstLastPeopleNaming = true;
        metadataSettings.Overrides = [MetadataSettingField.People];
        metadataSettings.PersonRoles = [PersonRole.Writer];
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Staff = [CreateStaff("John", "Doe Doe", "Story"), CreateStaff("Doe", "John Doe", "Story")]
        }, 1);

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);

        var allWriters = postSeries.Metadata.People.Where(p => p.Role == PersonRole.Writer).ToList();
        Assert.Equal(2, allWriters.Count);
    }

    #endregion

    #region People - Characters

    [Fact]
    public async Task People_Character_NoExisting_Off_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - People - Character";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = false;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Characters = [CreateCharacter("John", "Doe", CharacterRole.Main)]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal([], postSeries.Metadata.People.Where(p => p.Role == PersonRole.Character));
    }

    [Fact]
    public async Task People_Character_NoExisting_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - People - Character";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = true;
        metadataSettings.FirstLastPeopleNaming = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Characters = [CreateCharacter("John", "Doe", CharacterRole.Main)]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["John Doe"], postSeries.Metadata.People.Where(p => p.Role == PersonRole.Character).Select(p => p.Person.Name));
    }

    [Fact]
    public async Task People_Character_Locked_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, personLookup) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - People - Character";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithPerson(personLookup["Johnny Twowheeler"], PersonRole.Character)
                .Build())
            .Build();
        series.Metadata.CharacterLocked = true;
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = true;
        metadataSettings.FirstLastPeopleNaming = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Characters = [CreateCharacter("John", "Doe", CharacterRole.Main)]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[]{"Johnny Twowheeler"}.OrderBy(s => s),
            postSeries.Metadata.People.Where(p => p.Role == PersonRole.Character)
                .Select(p => p.Person.Name)
                .OrderBy(s => s));
    }

    [Fact]
    public async Task People_Character_Locked_Override_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, personLookup) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - People - Character";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithPerson(personLookup["Johnny Twowheeler"], PersonRole.Character)
                .Build())
            .Build();
        series.Metadata.WriterLocked = true;
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = true;
        metadataSettings.FirstLastPeopleNaming = true;
        metadataSettings.Overrides = [MetadataSettingField.People];
        metadataSettings.PersonRoles = [PersonRole.Character];
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Characters = [CreateCharacter("John", "Doe", CharacterRole.Main)]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[]{"John Doe", "Johnny Twowheeler"}.OrderBy(s => s),
            postSeries.Metadata.People.Where(p => p.Role == PersonRole.Character)
                .Select(p => p.Person.Name)
                .OrderBy(s => s));
        Assert.True( postSeries.Metadata.People.Where(p => p.Role == PersonRole.Character)
            .FirstOrDefault(p => p.Person.Name == "John Doe")!.KavitaPlusConnection);
    }

    [Fact]
    public async Task People_Character_Locked_Override_ReverseNamingNoMatch_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, personLookup) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - People - Character";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithPerson(personLookup["Johnny Twowheeler"], PersonRole.Character)
                .Build())
            .Build();
        series.Metadata.WriterLocked = true;
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = true;
        metadataSettings.FirstLastPeopleNaming = false;
        metadataSettings.Overrides = [MetadataSettingField.People];
        metadataSettings.PersonRoles = [PersonRole.Character];
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Characters = [CreateCharacter("Twowheeler", "Johnny", CharacterRole.Main)]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[]{"Johnny Twowheeler", "Twowheeler Johnny"}.OrderBy(s => s),
            postSeries.Metadata.People.Where(p => p.Role == PersonRole.Character)
                .Select(p => p.Person.Name)
                .OrderBy(s => s));
    }

    [Fact]
    public async Task People_Character_Locked_Override_PersonRoleNotSet_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, personLookup) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - People - Character";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithPerson(personLookup["Johnny Twowheeler"], PersonRole.Character)
                .Build())
            .Build();
        series.Metadata.WriterLocked = true;
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = true;
        metadataSettings.FirstLastPeopleNaming = true;
        metadataSettings.Overrides = [MetadataSettingField.People];
        metadataSettings.PersonRoles = [];
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Characters = [CreateCharacter("John", "Doe", CharacterRole.Main)]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[]{"Johnny Twowheeler"}.OrderBy(s => s),
            postSeries.Metadata.People.Where(p => p.Role == PersonRole.Character)
                .Select(p => p.Person.Name)
                .OrderBy(s => s));
    }


    [Fact]
    public async Task People_Character_OverrideReMatchDeletesOld_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - People - Character";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = true;
        metadataSettings.FirstLastPeopleNaming = true;
        metadataSettings.Overrides = [MetadataSettingField.People];
        metadataSettings.PersonRoles = [PersonRole.Character];
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Characters = [CreateCharacter("John", "Doe", CharacterRole.Main)]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[]{"John Doe"}.OrderBy(s => s),
            postSeries.Metadata.People.Where(p => p.Role == PersonRole.Character)
                .Select(p => p.Person.Name)
                .OrderBy(s => s));

        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Characters = [CreateCharacter("John", "Doe 2", CharacterRole.Main)]
        }, 1);

        postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[]{"John Doe 2"}.OrderBy(s => s),
            postSeries.Metadata.People.Where(p => p.Role == PersonRole.Character)
                .Select(p => p.Person.Name)
                .OrderBy(s => s));
    }

    #endregion

    #region Series Cover
    // Not sure how to test this
    #endregion

    #region Relationships

    // Not enabled

    // Non-Sequel

    [Fact]
    public async Task Relationships_NonSequel()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Relationships Side Story";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);

        var series2 = new SeriesBuilder("Test - Relationships Side Story - Target")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series2);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableRelationships = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Relations = [new SeriesRelationship()
            {
                Relation = RelationKind.SideStory,
                SeriesName = new ALMediaTitle()
                {
                    PreferredTitle = series2.Name,
                    EnglishTitle = null,
                    NativeTitle = series2.Name,
                    RomajiTitle = series2.Name,
                },
                AniListId = 10,
                Format = PlusMediaFormat.Manga
            }]
        }, 1);


        var sourceSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata | SeriesIncludes.Related);
        Assert.NotNull(sourceSeries);
        Assert.Single(sourceSeries.Relations);
        Assert.Equal(series2.Name, sourceSeries.Relations.First().TargetSeries.Name);
    }

    // Non-Sequel matched purely via an external metadata id - names intentionally do not match the target,
    // so the relationship can only resolve through the GetSeriesFromExternalMetadata id lookup.
    [Fact]
    public async Task Relationships_NonSequel_MatchByExternalId()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Relationships Source";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);

        var series2 = new SeriesBuilder("Test - Relationships Target")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithMangaBakaId(555)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series2);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableRelationships = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Relations = [new SeriesRelationship()
            {
                Relation = RelationKind.SideStory,
                // Names deliberately do not match series2 - resolution must happen via MangabakaId
                SeriesName = new ALMediaTitle()
                {
                    PreferredTitle = "No Name Match Whatsoever",
                    EnglishTitle = null,
                    NativeTitle = "No Name Match Whatsoever",
                    RomajiTitle = "No Name Match Whatsoever",
                },
                MangabakaId = 555,
                Format = PlusMediaFormat.Manga
            }]
        }, 1);

        // Repull Series and validate the relationship resolved via the external id
        var sourceSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata | SeriesIncludes.Related);
        Assert.NotNull(sourceSeries);
        Assert.Single(sourceSeries.Relations);
        Assert.Equal(series2.Name, sourceSeries.Relations.First().TargetSeries.Name);
    }

    [Fact]
    public async Task Relationships_NonSequel_LocalizedName()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Relationships Side Story";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);

        var series2 = new SeriesBuilder("Test - Relationships Side Story - Target")
            .WithLibraryId(1)
            .WithLocalizedName("School bus")
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series2);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableRelationships = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Relations = [new SeriesRelationship()
            {
                Relation = RelationKind.SideStory,
                SeriesName = new ALMediaTitle()
                {
                    PreferredTitle = "School bus",
                    EnglishTitle = null,
                    NativeTitle = series2.Name,
                    RomajiTitle = series2.Name,
                },
                AniListId = 10,
                Format = PlusMediaFormat.Manga
            }]
        }, 1);


        var sourceSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata | SeriesIncludes.Related);
        Assert.NotNull(sourceSeries);
        Assert.Single(sourceSeries.Relations);
        Assert.Equal(series2.Name, sourceSeries.Relations.First().TargetSeries.Name);
    }

    // Non-Sequel with no match due to Format difference
    [Fact]
    public async Task Relationships_NonSequel_FormatDifference()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Relationships Side Story";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);

        var series2 = new SeriesBuilder("Test - Relationships Side Story - Target")
            .WithLibraryId(1)
            .WithLocalizedName("School bus")
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series2);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableRelationships = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Relations = [new SeriesRelationship()
            {
                Relation = RelationKind.SideStory,
                SeriesName = new ALMediaTitle()
                {
                    PreferredTitle = "School bus",
                    EnglishTitle = null,
                    NativeTitle = series2.Name,
                    RomajiTitle = series2.Name,
                },
                AniListId = 10,
                Format = PlusMediaFormat.Book
            }]
        }, 1);


        var sourceSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata | SeriesIncludes.Related);
        Assert.NotNull(sourceSeries);
        Assert.Empty(sourceSeries.Relations);
    }

    // Non-Sequel existing relationship with new link, both exist
    [Fact]
    public async Task Relationships_NonSequel_ExistingLink_DifferentType_BothExist()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        var existingRelationshipSeries = new SeriesBuilder("Existing")
            .WithLibraryId(1)
            .Build();
        context.Series.Attach(existingRelationshipSeries);
        await context.SaveChangesAsync();

        const string seriesName = "Test - Relationships Side Story";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithRelationship(existingRelationshipSeries.Id, RelationKind.Annual)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);

        var series2 = new SeriesBuilder("Test - Relationships Side Story - Target")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series2);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableRelationships = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Relations = [new SeriesRelationship()
            {
                Relation = RelationKind.SideStory,
                SeriesName = new ALMediaTitle()
                {
                    PreferredTitle = series2.Name,
                    EnglishTitle = null,
                    NativeTitle = series2.Name,
                    RomajiTitle = series2.Name,
                },
                Format = PlusMediaFormat.Manga
            }]
        }, 2);


       var sourceSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(2, SeriesIncludes.Metadata | SeriesIncludes.Related);
        Assert.NotNull(sourceSeries);
        Assert.Equal(seriesName, sourceSeries.Name);

        Assert.Contains(sourceSeries.Relations, r => r.RelationKind == RelationKind.Annual && r.TargetSeriesId == existingRelationshipSeries.Id);
        Assert.Contains(sourceSeries.Relations, r => r.RelationKind == RelationKind.SideStory && r.TargetSeriesId == series2.Id);
    }



    // Sequel/Prequel
    [Fact]
    public async Task Relationships_Sequel_CreatesPrequel()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Relationships Source";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);

        var series2 = new SeriesBuilder("Test - Relationships Target")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series2);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableRelationships = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Relations = [new SeriesRelationship()
            {
                Relation = RelationKind.Sequel,
                SeriesName = new ALMediaTitle()
                {
                    PreferredTitle = series2.Name,
                    EnglishTitle = null,
                    NativeTitle = series2.Name,
                    RomajiTitle = series2.Name,
                },
                Format = PlusMediaFormat.Manga
            }]
        }, 1);


        var sourceSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata | SeriesIncludes.Related);
        Assert.NotNull(sourceSeries);
        Assert.Single(sourceSeries.Relations);
        Assert.Equal(series2.Name, sourceSeries.Relations.First().TargetSeries.Name);

        var sequel = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(2, SeriesIncludes.Metadata | SeriesIncludes.Related);
        Assert.NotNull(sequel);
        Assert.Equal(seriesName, sequel.Relations.First().TargetSeries.Name);
    }

    [Fact]
    public async Task Relationships_Prequel_CreatesSequel()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        // ID 1: Blue Lock - Episode Nagi
        var series = new SeriesBuilder("Blue Lock - Episode Nagi")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);

        // ID 2: Blue Lock
        var series2 = new SeriesBuilder("Blue Lock")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series2);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableRelationships = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        // Apply to Blue Lock - Episode Nagi (ID 1), setting Blue Lock (ID 2) as its prequel
        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = "Blue Lock - Episode Nagi", // The series we're updating metadata for
            Relations = [new SeriesRelationship()
            {
                Relation = RelationKind.Prequel, // Blue Lock is the prequel to Nagi
                SeriesName = new ALMediaTitle()
                {
                    PreferredTitle = "Blue Lock",
                    EnglishTitle = "Blue Lock",
                    NativeTitle = "ブルーロック",
                    RomajiTitle = "Blue Lock",
                },
                Format = PlusMediaFormat.Manga,
                AniListId = 106130,
                MalId = 114745,
                Provider = ScrobbleProvider.AniList
            }]
        }, 1); // Apply to series ID 1 (Nagi)

        // Verify Blue Lock - Episode Nagi has Blue Lock as prequel
        var nagiSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata | SeriesIncludes.Related);
        Assert.NotNull(nagiSeries);
        Assert.Single(nagiSeries.Relations);
        Assert.Equal("Blue Lock", nagiSeries.Relations.First().TargetSeries.Name);
        Assert.Equal(RelationKind.Prequel, nagiSeries.Relations.First().RelationKind);

        // Verify Blue Lock has Blue Lock - Episode Nagi as sequel
        var blueLockSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(2, SeriesIncludes.Metadata | SeriesIncludes.Related);
        Assert.NotNull(blueLockSeries);
        Assert.Single(blueLockSeries.Relations);
        Assert.Equal("Blue Lock - Episode Nagi", blueLockSeries.Relations.First().TargetSeries.Name);
        Assert.Equal(RelationKind.Sequel, blueLockSeries.Relations.First().RelationKind);
    }


    #endregion

    #region Blacklist

    [Fact]
    public async Task Blacklist_Genres()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Blacklist Genres";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = true;
        metadataSettings.EnableGenres = true;
        metadataSettings.Blacklist = ["Sports", "Action"];
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Boxing", "Sports", "Action"],
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[] {"Boxing"}.OrderBy(s => s), postSeries.Metadata.Genres.Select(t => t.Title).OrderBy(s => s));
    }


    [Fact]
    public async Task Blacklist_Tags()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Blacklist Tags";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = true;
        metadataSettings.EnableGenres = true;
        metadataSettings.Blacklist = ["Sports", "Action"];
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags = [new MetadataTagDto() {Name = "Boxing"}, new MetadataTagDto() {Name = "Sports"}, new MetadataTagDto() {Name = "Action"}]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[] {"Boxing"}.OrderBy(s => s), postSeries.Metadata.Tags.Select(t => t.Title).OrderBy(s => s));
    }

    // Blacklist Tag

    // Field Map then Blacklist Genre

    // Field Map then Blacklist Tag

    #endregion

    #region Whitelist

    [Fact]
    public async Task Whitelist_Tags()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Whitelist Tags";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = true;
        metadataSettings.Whitelist = ["Sports", "Action"];
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags = [new MetadataTagDto() {Name = "Boxing"}, new MetadataTagDto() {Name = "Sports"}, new MetadataTagDto() {Name = "Action"}]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[] {"Sports", "Action"}.OrderBy(s => s), postSeries.Metadata.Tags.Select(t => t.Title).OrderBy(s => s));
    }

    [Fact]
    public async Task Whitelist_WithFieldMap_Tags()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Whitelist Tags";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = true;
        metadataSettings.FieldMappings = [new MetadataFieldMapping()
        {
            SourceType = MetadataFieldType.Tag,
            SourceValue = "Boxing",
            DestinationType = MetadataFieldType.Tag,
            DestinationValue = "Sports",
            ExcludeFromSource = false

        }];
        metadataSettings.Whitelist = ["Sports", "Action"];
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags = [new MetadataTagDto() {Name = "Boxing"}, new MetadataTagDto() {Name = "Action"}]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[] {"Sports", "Action"}.OrderBy(s => s), postSeries.Metadata.Tags.Select(t => t.Title).OrderBy(s => s));
    }

    #endregion

    #region Field Mapping

    [Fact]
    public async Task FieldMap_GenreToGenre_KeepSource_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Genres Field Mapping";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableGenres = true;
        metadataSettings.Overrides = [MetadataSettingField.Genres];
        metadataSettings.FieldMappings = [new MetadataFieldMapping()
        {
            SourceType = MetadataFieldType.Genre,
            SourceValue = "Ecchi",
            DestinationType = MetadataFieldType.Genre,
            DestinationValue = "Fanservice",
            ExcludeFromSource = false

        }];

        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(
            new[] { "Ecchi", "Fanservice" }.OrderBy(s => s),
            postSeries.Metadata.Genres.Select(g => g.Title).OrderBy(s => s)
        );
    }

    [Fact]
    public async Task FieldMap_GenreToGenre_RemoveSource_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Genres Field Mapping";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableGenres = true;
        metadataSettings.Overrides = [MetadataSettingField.Genres];
        metadataSettings.FieldMappings = [new MetadataFieldMapping()
        {
            SourceType = MetadataFieldType.Genre,
            SourceValue = "Ecchi",
            DestinationType = MetadataFieldType.Genre,
            DestinationValue = "Fanservice",
            ExcludeFromSource = true

        }];

        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["Fanservice"], postSeries.Metadata.Genres.Select(g => g.Title));
    }

    [Fact]
    public async Task FieldMap_TagToTag_KeepSource_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Tag Field Mapping";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = true;
        metadataSettings.FieldMappings = [new MetadataFieldMapping()
        {
            SourceType = MetadataFieldType.Tag,
            SourceValue = "Ecchi",
            DestinationType = MetadataFieldType.Tag,
            DestinationValue = "Fanservice",
            ExcludeFromSource = false

        }];

        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags = [new MetadataTagDto() {Name = "Ecchi"}]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(
            new[] { "Ecchi", "Fanservice" }.OrderBy(s => s),
            postSeries.Metadata.Tags.Select(g => g.Title).OrderBy(s => s)
        );
    }

    [Fact]
    public async Task Tags_Existing_FieldMap_RemoveSource_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Tag Field Mapping";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = true;
        metadataSettings.Overrides = [MetadataSettingField.Genres];
        metadataSettings.FieldMappings = [new MetadataFieldMapping()
        {
            SourceType = MetadataFieldType.Tag,
            SourceValue = "Ecchi",
            DestinationType = MetadataFieldType.Tag,
            DestinationValue = "Fanservice",
            ExcludeFromSource = true

        }];

        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags = [new MetadataTagDto() {Name = "Ecchi"}]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["Fanservice"], postSeries.Metadata.Tags.Select(g => g.Title));
    }

    [Fact]
    public async Task FieldMap_GenreToTag_KeepSource_Modification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Genres Field Mapping";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableGenres = true;
        metadataSettings.EnableTags = true;
        metadataSettings.Overrides = [MetadataSettingField.Genres, MetadataSettingField.Tags];
        metadataSettings.FieldMappings = [new MetadataFieldMapping()
        {
            SourceType = MetadataFieldType.Genre,
            SourceValue = "Ecchi",
            DestinationType = MetadataFieldType.Tag,
            DestinationValue = "Fanservice",
            ExcludeFromSource = false

        }];

        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"]
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(
            new[] {"Ecchi"}.OrderBy(s => s),
            postSeries.Metadata.Genres.Select(g => g.Title).OrderBy(s => s)
        );
        Assert.Equal(
            new[] {"Fanservice"}.OrderBy(s => s),
            postSeries.Metadata.Tags.Select(g => g.Title).OrderBy(s => s)
        );
    }



    [Fact]
    public async Task FieldMap_GenreToGenre_RemoveSource_NoExternalGenre_NoModification()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, genreLookup, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Genres Field Mapping";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithGenre(genreLookup["Action"])
                .Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableGenres = true;
        metadataSettings.EnableTags = true;
        metadataSettings.Overrides = [MetadataSettingField.Genres, MetadataSettingField.Tags];
        metadataSettings.FieldMappings = [new MetadataFieldMapping()
        {
            SourceType = MetadataFieldType.Genre,
            SourceValue = "Action",
            DestinationType = MetadataFieldType.Genre,
            DestinationValue = "Adventure",
            ExcludeFromSource = true

        }];

        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();


        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
        }, 1);


        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(
            new[] {"Action"}.OrderBy(s => s),
            postSeries.Metadata.Genres.Select(g => g.Title).OrderBy(s => s)
        );
    }

    #endregion


    #region Name (Kavita+ write)

    [Fact]
    public async Task Name_Enabled_Unlocked_Writes()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        var series = new SeriesBuilder("Original Name")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableName = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = "New K+ Name",
            LocalizedTitles = new Dictionary<string, IList<LocalizedTitleDto>>
            {
                ["en"] = [new LocalizedTitleDto { Title = "New K+ Name" }]
            }
        }, 1);

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal("New K+ Name", postSeries.Name);
        Assert.Equal("New K+ Name".ToNormalized(), postSeries.NormalizedName);
        Assert.True(postSeries.NameLocked);
    }

    [Fact]
    public async Task Name_Locked_NoForceOverride_Skipped()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        var series = new SeriesBuilder("Original Name")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        series.NameLocked = true;
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableName = true;
        metadataSettings.Overrides = []; // no force override for Name
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = "New K+ Name",
            LocalizedTitles = new Dictionary<string, IList<LocalizedTitleDto>>
            {
                ["en"] = [new LocalizedTitleDto { Title = "New K+ Name" }]
            }
        }, 1);

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal("Original Name", postSeries.Name);
    }

    [Fact]
    public async Task Name_WouldCollide_Skipped()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, _, _, _) = await Setup(unitOfWork, context, mapper);

        // Existing series (Id 1) already owns the name K+ would try to write
        var existing = new SeriesBuilder("Existing Name")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        context.Series.Attach(existing);

        // Target series (Id 2) that K+ is writing to
        var target = new SeriesBuilder("Original Name")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        context.Series.Attach(target);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableName = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = "Existing Name",
            LocalizedTitles = new Dictionary<string, IList<LocalizedTitleDto>>
            {
                ["en"] = [new LocalizedTitleDto { Title = "Existing Name" }]
            }
        }, target.Id);

        var postSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(target.Id, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal("Original Name", postSeries.Name);
    }

    #endregion

    #region Idempotency

    /// <summary>
    /// Validates that 2 concurrent writes (browse series-detail then try to match) only writes one new Genre
    /// </summary>
    [Fact]
    public async Task WriteExternalMetadataToSeries_GenreAlreadyLinked_DoesNotDuplicate()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (externalMetadataService, genreLookup, _, _) = await Setup(unitOfWork, context, mapper);

        const string seriesName = "Test - Concurrent Genre";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        context.Series.Attach(series);
        await context.SaveChangesAsync();

        var metadataSettings = await unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableGenres = true;
        context.MetadataSettings.Update(metadataSettings);
        await context.SaveChangesAsync();

        var metadataId = (await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata))!.Metadata.Id;
        var actionGenreId = genreLookup["Action"].Id;
        context.ChangeTracker.Clear();

        // A prior/concurrent write already committed the "Action" link
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO GenreSeriesMetadata (GenresId, SeriesMetadatasId) VALUES ({0}, {1})", actionGenreId, metadataId);

        // Applying the same genre must not attempt to re-insert the existing join (UNIQUE constraint would throw)
        var ex = await Record.ExceptionAsync(async () =>
        {
            await externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
            {
                Name = seriesName,
                Genres = ["Action"]
            }, 1);
            await unitOfWork.CommitAsync();
        });
        Assert.Null(ex);

        // Exactly one Action link should exist in the database
        context.ChangeTracker.Clear();
        var linkCount = await context.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM GenreSeriesMetadata WHERE SeriesMetadatasId = {0}", metadataId)
            .SingleAsync();
        Assert.Equal(1, linkCount);
    }

    #endregion

    private static SeriesStaffDto CreateStaff(string first, string last, string role)
    {
        return new SeriesStaffDto() {Name = $"{first} {last}", Role = role, Url = "", FirstName = first, LastName = last};
    }

    private static SeriesCharacter CreateCharacter(string first, string last, CharacterRole role)
    {
        return new SeriesCharacter() {Name = $"{first} {last}", Description = "", Url = "", ImageUrl = "", Role = role};
    }
}
