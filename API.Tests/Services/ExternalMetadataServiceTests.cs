using System;
using System.IO;
using System.Threading.Tasks;
using API.Constants;
using API.Data.Repositories;
using API.DTOs.Recommendation;
using API.Entities;
using API.Entities.Metadata;
using API.Helpers.Builders;
using API.Services.Plus;
using API.Services.Tasks.Metadata;
using API.SignalR;
using Hangfire;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services;

/// <summary>
/// Given these rely on Kavita+, this will not have any [Fact]/[Theory] on them and must be manually checked
/// </summary>
public class ExternalMetadataServiceTests : AbstractDbTest
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly ExternalMetadataService _externalMetadataService;


    public ExternalMetadataServiceTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;

        // Set up Hangfire to use in-memory storage for testing
        GlobalConfiguration.Configuration.UseInMemoryStorage();

        _externalMetadataService = new ExternalMetadataService(_unitOfWork, Substitute.For<ILogger<ExternalMetadataService>>(),
            _mapper, Substitute.For<ILicenseService>(), Substitute.For<IScrobblingService>(), Substitute.For<IEventHub>(),
            Substitute.For<ICoverDbService>());
    }

    #region Summary

    [Fact]
    public async Task Summary_NoExisting_Modification()
    {
        await ResetDb();

        const string seriesName = "Test - Summary";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableSummary = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Summary = "Test"
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.False(string.IsNullOrEmpty(postSeries.Metadata.Summary));
        Assert.Equal(series.Metadata.Summary, postSeries.Metadata.Summary);
    }

    [Fact]
    public async Task Summary_Existing_NoModification()
    {
        await ResetDb();

        const string seriesName = "Test - Summary";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithSummary("This summary is not locked")
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableSummary = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Summary = "This should not write"
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.False(string.IsNullOrEmpty(postSeries.Metadata.Summary));
        Assert.Equal("This summary is not locked", postSeries.Metadata.Summary);
    }

    [Fact]
    public async Task Summary_Existing_Locked_NoModification()
    {
        await ResetDb();

        const string seriesName = "Test - Summary";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithSummary("This summary is not locked", true)
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableSummary = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Summary = "This should not write"
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.False(string.IsNullOrEmpty(postSeries.Metadata.Summary));
        Assert.Equal("This summary is not locked", postSeries.Metadata.Summary);
    }

    [Fact]
    public async Task Summary_Existing_Locked_Override_Modification()
    {
        await ResetDb();

        const string seriesName = "Test - Summary";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithSummary("This summary is not locked", true)
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableSummary = true;
        metadataSettings.Overrides = [MetadataSettingField.Summary];
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Summary = "This should write"
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.False(string.IsNullOrEmpty(postSeries.Metadata.Summary));
        Assert.Equal("This should write", postSeries.Metadata.Summary);
    }


    #endregion

    #region Release Year

    [Fact]
    public async Task ReleaseYear_NoExisting_Modification()
    {
        await ResetDb();

        const string seriesName = "Test - Release Year";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableStartDate = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            StartDate = DateTime.UtcNow
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(DateTime.UtcNow.Year, postSeries.Metadata.ReleaseYear);
    }

    [Fact]
    public async Task ReleaseYear_Existing_NoModification()
    {
        await ResetDb();

        const string seriesName = "Test - Release Year";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithReleaseYear(1990)
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableStartDate = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            StartDate = DateTime.UtcNow
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(1990, postSeries.Metadata.ReleaseYear);
    }

    [Fact]
    public async Task ReleaseYear_Existing_Locked_NoModification()
    {
        await ResetDb();

        const string seriesName = "Test - Release Year";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithReleaseYear(1990, true)
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableStartDate = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            StartDate = DateTime.UtcNow
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(1990, postSeries.Metadata.ReleaseYear);
    }

    [Fact]
    public async Task ReleaseYear_Existing_Locked_Override_Modification()
    {
        await ResetDb();

        const string seriesName = "Test - Release Year";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithReleaseYear(1990, true)
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableStartDate = true;
        metadataSettings.Overrides = [MetadataSettingField.StartDate];
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            StartDate = DateTime.UtcNow
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(DateTime.UtcNow.Year, postSeries.Metadata.ReleaseYear);
    }

    #endregion

    #region LocalizedName

    [Fact]
    public async Task LocalizedName_NoExisting_Modification()
    {
        await ResetDb();

        const string seriesName = "Test - Localized Name";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithLocalizedNameAllowEmpty(string.Empty)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableLocalizedName = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Synonyms = [seriesName, "設定しないでください", "Kimchi"]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal("Kimchi", postSeries.LocalizedName);
    }

    [Fact]
    public async Task LocalizedName_Existing_NoModification()
    {
        await ResetDb();

        const string seriesName = "Test - Localized Name";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithLocalizedName("Localized Name here")
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableLocalizedName = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Synonyms = [seriesName, "設定しないでください", "Kimchi"]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal("Localized Name here", postSeries.LocalizedName);
    }

    [Fact]
    public async Task LocalizedName_Existing_Locked_NoModification()
    {
        await ResetDb();

        const string seriesName = "Test - Localized Name";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithLocalizedName("Localized Name here", true)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableLocalizedName = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Synonyms = [seriesName, "設定しないでください", "Kimchi"]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal("Localized Name here", postSeries.LocalizedName);
    }

    [Fact]
    public async Task LocalizedName_Existing_Locked_Override_Modification()
    {
        await ResetDb();

        const string seriesName = "Test - Localized Name";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithLocalizedName("Localized Name here", true)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableLocalizedName = true;
        metadataSettings.Overrides = [MetadataSettingField.LocalizedName];
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Synonyms = [seriesName, "設定しないでください", "Kimchi"]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal("Kimchi", postSeries.LocalizedName);
    }

    [Fact]
    public async Task LocalizedName_OnlyNonEnglishSynonyms_Modification()
    {
        await ResetDb();

        const string seriesName = "Test - Localized Name";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithLocalizedNameAllowEmpty(string.Empty)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableLocalizedName = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Synonyms = [seriesName, "設定しないでください"]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.True(string.IsNullOrEmpty(postSeries.LocalizedName));
    }

    #endregion

    #region Publication Status
    #endregion

    #region Age Rating
    #endregion

    #region Genres
    #endregion

    #region Tags
    #endregion

    #region People - Writers/Artists
    #endregion

    #region People - Characters
    #endregion

    #region Series Cover
    #endregion

    #region Relationships
    #endregion






    protected override async Task ResetDb()
    {
       _context.Series.RemoveRange(_context.Series);
       _context.AppUser.RemoveRange(_context.AppUser);

       var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
       metadataSettings.Enabled = false;
       metadataSettings.EnableSummary = false;
       metadataSettings.EnableCoverImage = false;
       metadataSettings.EnableLocalizedName = false;
       metadataSettings.EnableGenres = false;
       metadataSettings.EnablePeople = false;
       metadataSettings.EnableRelationships = false;
       metadataSettings.EnableTags = false;
       metadataSettings.EnablePublicationStatus = false;
       metadataSettings.EnableStartDate = false;
       _context.MetadataSettings.Update(metadataSettings);

       await _context.SaveChangesAsync();

       _context.AppUser.Add(new AppUserBuilder("Joe", "Joe").WithRole(PolicyConstants.AdminRole).Build());

       await _context.SaveChangesAsync();
    }
}
