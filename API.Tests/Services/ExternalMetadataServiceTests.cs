using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data.Repositories;
using API.DTOs.KavitaPlus.Metadata;
using API.DTOs.Recommendation;
using API.DTOs.Scrobbling;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Helpers.Builders;
using API.Services.Plus;
using API.Services.Tasks.Metadata;
using API.SignalR;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;
using YamlDotNet.Serialization;

namespace API.Tests.Services;

/// <summary>
/// Given these rely on Kavita+, this will not have any [Fact]/[Theory] on them and must be manually checked
/// </summary>
public class ExternalMetadataServiceTests : AbstractDbTest
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly ExternalMetadataService _externalMetadataService;
    private readonly Dictionary<string, Genre> _genreLookup = new Dictionary<string, Genre>();
    private readonly Dictionary<string, Tag> _tagLookup = new Dictionary<string, Tag>();
    private readonly Dictionary<string, Person> _personLookup = new Dictionary<string, Person>();


    public ExternalMetadataServiceTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;

        // Set up Hangfire to use in-memory storage for testing
        GlobalConfiguration.Configuration.UseInMemoryStorage();

        _externalMetadataService = new ExternalMetadataService(_unitOfWork, Substitute.For<ILogger<ExternalMetadataService>>(),
            _mapper, Substitute.For<ILicenseService>(), Substitute.For<IScrobblingService>(), Substitute.For<IEventHub>(),
            Substitute.For<ICoverDbService>());
    }

    #region Gloabl

    [Fact]
    public async Task Off_Modification()
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
        metadataSettings.Enabled = false;
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
        Assert.Equal(string.Empty, postSeries.Metadata.Summary);
    }

    #endregion

    #region Summary

    [Fact]
    public async Task Summary_NoExisting_Off_Modification()
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
        metadataSettings.EnableSummary = false;
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
        Assert.Equal(string.Empty, postSeries.Metadata.Summary);
    }

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
    public async Task ReleaseYear_NoExisting_Off_NoModification()
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
        metadataSettings.EnableStartDate = false;
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
        Assert.Equal(0, postSeries.Metadata.ReleaseYear);
    }

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
    public async Task LocalizedName_NoExisting_Off_Modification()
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
        metadataSettings.EnableLocalizedName = false;
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
        Assert.Equal(string.Empty, postSeries.LocalizedName);
    }

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

    [Fact]
    public async Task PublicationStatus_NoExisting_Off_NoModification()
    {
        await ResetDb();

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
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePublicationStatus = false;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Volumes = 2
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(PublicationStatus.OnGoing, postSeries.Metadata.PublicationStatus);
    }

    [Fact]
    public async Task PublicationStatus_NoExisting_Modification()
    {
        await ResetDb();

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
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePublicationStatus = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Volumes = 2
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(PublicationStatus.Completed, postSeries.Metadata.PublicationStatus);
    }

    [Fact]
    public async Task PublicationStatus_Existing_Modification()
    {
        await ResetDb();

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
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePublicationStatus = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Volumes = 2
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(PublicationStatus.Completed, postSeries.Metadata.PublicationStatus);
    }

    [Fact]
    public async Task PublicationStatus_Existing_Locked_NoModification()
    {
        await ResetDb();

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
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePublicationStatus = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Volumes = 2
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(PublicationStatus.Hiatus, postSeries.Metadata.PublicationStatus);
    }

    [Fact]
    public async Task PublicationStatus_Existing_Locked_Override_Modification()
    {
        await ResetDb();

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
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePublicationStatus = true;
        metadataSettings.Overrides = [MetadataSettingField.PublicationStatus];
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Volumes = 2
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(PublicationStatus.Completed, postSeries.Metadata.PublicationStatus);
    }

    [Fact]
    public async Task PublicationStatus_Existing_CorrectState_Modification()
    {
        await ResetDb();

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
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePublicationStatus = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Volumes = 2
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(PublicationStatus.Ended, postSeries.Metadata.PublicationStatus);
    }



    #endregion

    #region Age Rating

    [Fact]
    public async Task AgeRating_NoExisting_Modification()
    {
        await ResetDb();

        const string seriesName = "Test - Age Rating";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.AgeRatingMappings = new Dictionary<string, AgeRating>()
        {
            {"Ecchi", AgeRating.Teen}, // Genre
            {"H", AgeRating.R18Plus}, // Tag
        };
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(AgeRating.Teen, postSeries.Metadata.AgeRating);
    }

    [Fact]
    public async Task AgeRating_ExistingHigher_NoModification()
    {
        await ResetDb();

        const string seriesName = "Test - Age Rating";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithAgeRating(AgeRating.Mature)
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.AgeRatingMappings = new Dictionary<string, AgeRating>()
        {
            {"Ecchi", AgeRating.Teen}, // Genre
            {"H", AgeRating.R18Plus}, // Tag
        };
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(AgeRating.Mature, postSeries.Metadata.AgeRating);
    }

    [Fact]
    public async Task AgeRating_ExistingLower_Modification()
    {
        await ResetDb();

        const string seriesName = "Test - Age Rating";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithAgeRating(AgeRating.Everyone)
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.AgeRatingMappings = new Dictionary<string, AgeRating>()
        {
            {"Ecchi", AgeRating.Teen}, // Genre
            {"H", AgeRating.R18Plus}, // Tag
        };
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(AgeRating.Teen, postSeries.Metadata.AgeRating);
    }

    [Fact]
    public async Task AgeRating_Existing_Locked_NoModification()
    {
        await ResetDb();

        const string seriesName = "Test - Age Rating";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithAgeRating(AgeRating.Everyone, true)
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.AgeRatingMappings = new Dictionary<string, AgeRating>()
        {
            {"Ecchi", AgeRating.Teen}, // Genre
            {"H", AgeRating.R18Plus}, // Tag
        };
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(AgeRating.Everyone, postSeries.Metadata.AgeRating);
    }

    [Fact]
    public async Task AgeRating_Existing_Locked_Override_Modification()
    {
        await ResetDb();

        const string seriesName = "Test - Age Rating";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithAgeRating(AgeRating.Everyone, true)
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.Overrides = [MetadataSettingField.AgeRating];
        metadataSettings.AgeRatingMappings = new Dictionary<string, AgeRating>()
        {
            {"Ecchi", AgeRating.Teen}, // Genre
            {"H", AgeRating.R18Plus}, // Tag
        };
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(AgeRating.Teen, postSeries.Metadata.AgeRating);
    }

    #endregion

    #region Genres

    [Fact]
    public async Task Genres_NoExisting_Off_NoModification()
    {
        await ResetDb();

        const string seriesName = "Test - Genres";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableGenres = false;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal([], postSeries.Metadata.Genres);
    }

    [Fact]
    public async Task Genres_NoExisting_Modification()
    {
        await ResetDb();

        const string seriesName = "Test - Genres";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableGenres = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["Ecchi"], postSeries.Metadata.Genres.Select(g => g.Title));
    }

    [Fact]
    public async Task Genres_Existing_Modification()
    {
        await ResetDb();

        const string seriesName = "Test - Genres";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithGenre(_genreLookup["Action"])
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableGenres = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["Ecchi"], postSeries.Metadata.Genres.Select(g => g.Title));
    }

    [Fact]
    public async Task Genres_Existing_Locked_NoModification()
    {
        await ResetDb();

        const string seriesName = "Test - Genres";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithGenre(_genreLookup["Action"], true)
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableGenres = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["Action"], postSeries.Metadata.Genres.Select(g => g.Title));
    }

    [Fact]
    public async Task Genres_Existing_Locked_Override_Modification()
    {
        await ResetDb();

        const string seriesName = "Test - Genres";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithGenre(_genreLookup["Action"], true)
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableGenres = true;
        metadataSettings.Overrides = [MetadataSettingField.Genres];
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["Ecchi"], postSeries.Metadata.Genres.Select(g => g.Title));
    }

    #endregion

    #region Tags

    [Fact]
    public async Task Tags_NoExisting_Off_NoModification()
    {
        await ResetDb();

        const string seriesName = "Test - Tags";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = false;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags = [new MetadataTagDto() {Name = "Boxing"}]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal([], postSeries.Metadata.Tags);
    }

    [Fact]
    public async Task Tags_NoExisting_Modification()
    {
        await ResetDb();

        const string seriesName = "Test - Tags";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags = [new MetadataTagDto() {Name = "Boxing"}]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["Boxing"], postSeries.Metadata.Tags.Select(t => t.Title));
    }

    [Fact]
    public async Task Tags_Existing_Locked_NoModification()
    {
        await ResetDb();

        const string seriesName = "Test - Tags";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithTag(_tagLookup["H"], true)
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags = [new MetadataTagDto() {Name = "Boxing"}]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["H"], postSeries.Metadata.Tags.Select(t => t.Title));
    }

    [Fact]
    public async Task Tags_Existing_Locked_Override_Modification()
    {
        await ResetDb();

        const string seriesName = "Test - Tags";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithTag(_tagLookup["H"], true)
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = true;
        metadataSettings.Overrides = [MetadataSettingField.Tags];
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags = [new MetadataTagDto() {Name = "Boxing"}]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["Boxing"], postSeries.Metadata.Tags.Select(t => t.Title));
    }

    #endregion

    #region People - Writers/Artists

    [Fact]
    public async Task People_Writer_NoExisting_Off_NoModification()
    {
        await ResetDb();

        const string seriesName = "Test - People - Writer/Artists";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = false;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Staff = [CreateStaff("John", "Doe", "Story")]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal([], postSeries.Metadata.People.Where(p => p.Role == PersonRole.Writer));
    }

    [Fact]
    public async Task People_Writer_NoExisting_Modification()
    {
        await ResetDb();

        const string seriesName = "Test - People - Writer/Artists";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = true;
        metadataSettings.FirstLastPeopleNaming = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Staff = [CreateStaff("John", "Doe", "Story")]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["John Doe"], postSeries.Metadata.People.Where(p => p.Role == PersonRole.Writer).Select(p => p.Person.Name));
    }

    [Fact]
    public async Task People_Writer_Locked_NoModification()
    {
        await ResetDb();

        const string seriesName = "Test - People - Writer/Artists";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithPerson(_personLookup["Johnny Twowheeler"], PersonRole.Writer)
                .Build())
            .Build();
        series.Metadata.WriterLocked = true;
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = true;
        metadataSettings.FirstLastPeopleNaming = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Staff = [CreateStaff("John", "Doe", "Story")]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[]{"Johnny Twowheeler"}.OrderBy(s => s),
            postSeries.Metadata.People.Where(p => p.Role == PersonRole.Writer)
                .Select(p => p.Person.Name)
                .OrderBy(s => s));
    }

    [Fact]
    public async Task People_Writer_Locked_Override_Modification()
    {
        await ResetDb();

        const string seriesName = "Test - People - Writer/Artists";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithPerson(_personLookup["Johnny Twowheeler"], PersonRole.Writer)
                .Build())
            .Build();
        series.Metadata.WriterLocked = true;
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = true;
        metadataSettings.FirstLastPeopleNaming = true;
        metadataSettings.Overrides = [MetadataSettingField.People];
        metadataSettings.PersonRoles = [PersonRole.Writer];
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Staff = [CreateStaff("John", "Doe", "Story")]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
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
        await ResetDb();

        const string seriesName = "Test - People - Writer/Artists";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithPerson(_personLookup["Johnny Twowheeler"], PersonRole.Writer)
                .Build())
            .Build();
        series.Metadata.WriterLocked = true;
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = true;
        metadataSettings.FirstLastPeopleNaming = false;
        metadataSettings.Overrides = [MetadataSettingField.People];
        metadataSettings.PersonRoles = [PersonRole.Writer];
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Staff = [CreateStaff("Twowheeler", "Johnny", "Story")]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[]{"Johnny Twowheeler"}.OrderBy(s => s),
            postSeries.Metadata.People.Where(p => p.Role == PersonRole.Writer)
                .Select(p => p.Person.Name)
                .OrderBy(s => s));
    }

    [Fact]
    public async Task People_Writer_Locked_Override_PersonRoleNotSet_NoModification()
    {
        await ResetDb();

        const string seriesName = "Test - People - Writer/Artists";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithPerson(_personLookup["Johnny Twowheeler"], PersonRole.Writer)
                .Build())
            .Build();
        series.Metadata.WriterLocked = true;
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = true;
        metadataSettings.FirstLastPeopleNaming = true;
        metadataSettings.Overrides = [MetadataSettingField.People];
        metadataSettings.PersonRoles = [];
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Staff = [CreateStaff("John", "Doe", "Story")]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[]{"Johnny Twowheeler"}.OrderBy(s => s),
            postSeries.Metadata.People.Where(p => p.Role == PersonRole.Writer)
                .Select(p => p.Person.Name)
                .OrderBy(s => s));
    }


    [Fact]
    public async Task People_Writer_OverrideReMatchDeletesOld_Modification()
    {
        await ResetDb();

        const string seriesName = "Test - People - Writer/Artists";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = true;
        metadataSettings.FirstLastPeopleNaming = true;
        metadataSettings.Overrides = [MetadataSettingField.People];
        metadataSettings.PersonRoles = [PersonRole.Writer];
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Staff = [CreateStaff("John", "Doe", "Story")]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[]{"John Doe"}.OrderBy(s => s),
            postSeries.Metadata.People.Where(p => p.Role == PersonRole.Writer)
                .Select(p => p.Person.Name)
                .OrderBy(s => s));

        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Staff = [CreateStaff("John", "Doe 2", "Story")]
        }, 1);

        postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[]{"John Doe 2"}.OrderBy(s => s),
            postSeries.Metadata.People.Where(p => p.Role == PersonRole.Writer)
                .Select(p => p.Person.Name)
                .OrderBy(s => s));
    }

    #endregion

    #region People - Characters

    [Fact]
    public async Task People_Character_NoExisting_Off_NoModification()
    {
        await ResetDb();

        const string seriesName = "Test - People - Character";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = false;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Characters = [CreateCharacter("John", "Doe", CharacterRole.Main)]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal([], postSeries.Metadata.People.Where(p => p.Role == PersonRole.Character));
    }

    [Fact]
    public async Task People_Character_NoExisting_Modification()
    {
        await ResetDb();

        const string seriesName = "Test - People - Character";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = true;
        metadataSettings.FirstLastPeopleNaming = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Characters = [CreateCharacter("John", "Doe", CharacterRole.Main)]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["John Doe"], postSeries.Metadata.People.Where(p => p.Role == PersonRole.Character).Select(p => p.Person.Name));
    }

    [Fact]
    public async Task People_Character_Locked_NoModification()
    {
        await ResetDb();

        const string seriesName = "Test - People - Character";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithPerson(_personLookup["Johnny Twowheeler"], PersonRole.Character)
                .Build())
            .Build();
        series.Metadata.CharacterLocked = true;
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = true;
        metadataSettings.FirstLastPeopleNaming = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Characters = [CreateCharacter("John", "Doe", CharacterRole.Main)]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[]{"Johnny Twowheeler"}.OrderBy(s => s),
            postSeries.Metadata.People.Where(p => p.Role == PersonRole.Character)
                .Select(p => p.Person.Name)
                .OrderBy(s => s));
    }

    [Fact]
    public async Task People_Character_Locked_Override_Modification()
    {
        await ResetDb();

        const string seriesName = "Test - People - Character";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithPerson(_personLookup["Johnny Twowheeler"], PersonRole.Character)
                .Build())
            .Build();
        series.Metadata.WriterLocked = true;
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = true;
        metadataSettings.FirstLastPeopleNaming = true;
        metadataSettings.Overrides = [MetadataSettingField.People];
        metadataSettings.PersonRoles = [PersonRole.Character];
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Characters = [CreateCharacter("John", "Doe", CharacterRole.Main)]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
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
        await ResetDb();

        const string seriesName = "Test - People - Character";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithPerson(_personLookup["Johnny Twowheeler"], PersonRole.Character)
                .Build())
            .Build();
        series.Metadata.WriterLocked = true;
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = true;
        metadataSettings.FirstLastPeopleNaming = false;
        metadataSettings.Overrides = [MetadataSettingField.People];
        metadataSettings.PersonRoles = [PersonRole.Character];
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Characters = [CreateCharacter("Twowheeler", "Johnny", CharacterRole.Main)]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[]{"Johnny Twowheeler", "Twowheeler Johnny"}.OrderBy(s => s),
            postSeries.Metadata.People.Where(p => p.Role == PersonRole.Character)
                .Select(p => p.Person.Name)
                .OrderBy(s => s));
    }

    [Fact]
    public async Task People_Character_Locked_Override_PersonRoleNotSet_NoModification()
    {
        await ResetDb();

        const string seriesName = "Test - People - Character";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithPerson(_personLookup["Johnny Twowheeler"], PersonRole.Character)
                .Build())
            .Build();
        series.Metadata.WriterLocked = true;
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = true;
        metadataSettings.FirstLastPeopleNaming = true;
        metadataSettings.Overrides = [MetadataSettingField.People];
        metadataSettings.PersonRoles = [];
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Characters = [CreateCharacter("John", "Doe", CharacterRole.Main)]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[]{"Johnny Twowheeler"}.OrderBy(s => s),
            postSeries.Metadata.People.Where(p => p.Role == PersonRole.Character)
                .Select(p => p.Person.Name)
                .OrderBy(s => s));
    }


    [Fact]
    public async Task People_Character_OverrideReMatchDeletesOld_Modification()
    {
        await ResetDb();

        const string seriesName = "Test - People - Character";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnablePeople = true;
        metadataSettings.FirstLastPeopleNaming = true;
        metadataSettings.Overrides = [MetadataSettingField.People];
        metadataSettings.PersonRoles = [PersonRole.Character];
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Characters = [CreateCharacter("John", "Doe", CharacterRole.Main)]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[]{"John Doe"}.OrderBy(s => s),
            postSeries.Metadata.People.Where(p => p.Role == PersonRole.Character)
                .Select(p => p.Person.Name)
                .OrderBy(s => s));

        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Characters = [CreateCharacter("John", "Doe 2", CharacterRole.Main)]
        }, 1);

        postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
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
        await ResetDb();

        const string seriesName = "Test - Relationships Side Story";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);

        var series2 = new SeriesBuilder("Test - Relationships Side Story - Target")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .WithExternalMetadata(new ExternalSeriesMetadata()
            {
                AniListId = 10
            })
            .Build();
        _context.Series.Attach(series2);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableRelationships = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();

        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
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
                PlusMediaFormat = PlusMediaFormat.Manga
            }]
        }, 1);

        // Repull Series and validate what is overwritten
        var sourceSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata | SeriesIncludes.Related);
        Assert.NotNull(sourceSeries);
        Assert.Single(sourceSeries.Relations);
        Assert.Equal(series2.Name, sourceSeries.Relations.First().TargetSeries.Name);
    }

    [Fact]
    public async Task Relationships_NonSequel_LocalizedName()
    {
        await ResetDb();

        const string seriesName = "Test - Relationships Side Story";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);

        var series2 = new SeriesBuilder("Test - Relationships Side Story - Target")
            .WithLibraryId(1)
            .WithLocalizedName("School bus")
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series2);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableRelationships = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();

        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
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
                PlusMediaFormat = PlusMediaFormat.Manga
            }]
        }, 1);

        // Repull Series and validate what is overwritten
        var sourceSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata | SeriesIncludes.Related);
        Assert.NotNull(sourceSeries);
        Assert.Single(sourceSeries.Relations);
        Assert.Equal(series2.Name, sourceSeries.Relations.First().TargetSeries.Name);
    }

    // Non-Sequel with no match due to Format difference
    [Fact]
    public async Task Relationships_NonSequel_FormatDifference()
    {
        await ResetDb();

        const string seriesName = "Test - Relationships Side Story";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);

        var series2 = new SeriesBuilder("Test - Relationships Side Story - Target")
            .WithLibraryId(1)
            .WithLocalizedName("School bus")
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series2);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableRelationships = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();

        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
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
                PlusMediaFormat = PlusMediaFormat.Book
            }]
        }, 1);

        // Repull Series and validate what is overwritten
        var sourceSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata | SeriesIncludes.Related);
        Assert.NotNull(sourceSeries);
        Assert.Empty(sourceSeries.Relations);
    }

    // Non-Sequel existing relationship with new link, both exist
    [Fact]
    public async Task Relationships_NonSequel_ExistingLink_DifferentType_BothExist()
    {
        await ResetDb();

        var existingRelationshipSeries = new SeriesBuilder("Existing")
            .WithLibraryId(1)
            .Build();
        _context.Series.Attach(existingRelationshipSeries);
        await _context.SaveChangesAsync();

        const string seriesName = "Test - Relationships Side Story";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithRelationship(existingRelationshipSeries.Id, RelationKind.Annual)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);

        var series2 = new SeriesBuilder("Test - Relationships Side Story - Target")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .WithExternalMetadata(new ExternalSeriesMetadata()
            {
                AniListId = 10
            })
            .Build();
        _context.Series.Attach(series2);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableRelationships = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
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
                PlusMediaFormat = PlusMediaFormat.Manga
            }]
        }, 2);

        // Repull Series and validate what is overwritten
       var sourceSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(2, SeriesIncludes.Metadata | SeriesIncludes.Related);
        Assert.NotNull(sourceSeries);
        Assert.Equal(seriesName, sourceSeries.Name);

        Assert.Contains(sourceSeries.Relations, r => r.RelationKind == RelationKind.Annual && r.TargetSeriesId == existingRelationshipSeries.Id);
        Assert.Contains(sourceSeries.Relations, r => r.RelationKind == RelationKind.SideStory && r.TargetSeriesId == series2.Id);
    }



    // Sequel/Prequel
    [Fact]
    public async Task Relationships_Sequel_CreatesPrequel()
    {
        await ResetDb();

        const string seriesName = "Test - Relationships Source";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);

        var series2 = new SeriesBuilder("Test - Relationships Target")
            .WithLibraryId(1)
            .WithFormat(MangaFormat.Archive)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series2);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableRelationships = true;
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();

        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
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
                PlusMediaFormat = PlusMediaFormat.Manga
            }]
        }, 1);

        // Repull Series and validate what is overwritten
        var sourceSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata | SeriesIncludes.Related);
        Assert.NotNull(sourceSeries);
        Assert.Single(sourceSeries.Relations);
        Assert.Equal(series2.Name, sourceSeries.Relations.First().TargetSeries.Name);

        var sequel = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(2, SeriesIncludes.Metadata | SeriesIncludes.Related);
        Assert.NotNull(sequel);
        Assert.Equal(seriesName, sequel.Relations.First().TargetSeries.Name);
    }


    #endregion

    #region Blacklist

    [Fact]
    public async Task Blacklist_Genres()
    {
        await ResetDb();

        const string seriesName = "Test - Blacklist Genres";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = true;
        metadataSettings.EnableGenres = true;
        metadataSettings.Blacklist = ["Sports", "Action"];
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Boxing", "Sports", "Action"],
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[] {"Boxing"}.OrderBy(s => s), postSeries.Metadata.Genres.Select(t => t.Title).OrderBy(s => s));
    }


    [Fact]
    public async Task Blacklist_Tags()
    {
        await ResetDb();

        const string seriesName = "Test - Blacklist Tags";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = true;
        metadataSettings.EnableGenres = true;
        metadataSettings.Blacklist = ["Sports", "Action"];
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags = [new MetadataTagDto() {Name = "Boxing"}, new MetadataTagDto() {Name = "Sports"}, new MetadataTagDto() {Name = "Action"}]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
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
        await ResetDb();

        const string seriesName = "Test - Whitelist Tags";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        metadataSettings.Enabled = true;
        metadataSettings.EnableTags = true;
        metadataSettings.Whitelist = ["Sports", "Action"];
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags = [new MetadataTagDto() {Name = "Boxing"}, new MetadataTagDto() {Name = "Sports"}, new MetadataTagDto() {Name = "Action"}]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[] {"Sports", "Action"}.OrderBy(s => s), postSeries.Metadata.Tags.Select(t => t.Title).OrderBy(s => s));
    }

    [Fact]
    public async Task Whitelist_WithFieldMap_Tags()
    {
        await ResetDb();

        const string seriesName = "Test - Whitelist Tags";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
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
        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags = [new MetadataTagDto() {Name = "Boxing"}, new MetadataTagDto() {Name = "Action"}]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(new[] {"Sports", "Action"}.OrderBy(s => s), postSeries.Metadata.Tags.Select(t => t.Title).OrderBy(s => s));
    }

    #endregion

    #region Field Mapping

    [Fact]
    public async Task FieldMap_GenreToGenre_KeepSource_Modification()
    {
        await ResetDb();

        const string seriesName = "Test - Genres Field Mapping";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
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

        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(
            new[] { "Ecchi", "Fanservice" }.OrderBy(s => s),
            postSeries.Metadata.Genres.Select(g => g.Title).OrderBy(s => s)
        );
    }

    [Fact]
    public async Task FieldMap_GenreToGenre_RemoveSource_Modification()
    {
        await ResetDb();

        const string seriesName = "Test - Genres Field Mapping";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
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

        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["Fanservice"], postSeries.Metadata.Genres.Select(g => g.Title));
    }

    [Fact]
    public async Task FieldMap_TagToTag_KeepSource_Modification()
    {
        await ResetDb();

        const string seriesName = "Test - Tag Field Mapping";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
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

        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags = [new MetadataTagDto() {Name = "Ecchi"}]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(
            new[] { "Ecchi", "Fanservice" }.OrderBy(s => s),
            postSeries.Metadata.Tags.Select(g => g.Title).OrderBy(s => s)
        );
    }

    [Fact]
    public async Task Tags_Existing_FieldMap_RemoveSource_Modification()
    {
        await ResetDb();

        const string seriesName = "Test - Tag Field Mapping";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
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

        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Tags = [new MetadataTagDto() {Name = "Ecchi"}]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(["Fanservice"], postSeries.Metadata.Tags.Select(g => g.Title));
    }

    [Fact]
    public async Task FieldMap_GenreToTag_KeepSource_Modification()
    {
        await ResetDb();

        const string seriesName = "Test - Genres Field Mapping";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
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

        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
            Genres = ["Ecchi"]
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
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
        await ResetDb();

        const string seriesName = "Test - Genres Field Mapping";
        var series = new SeriesBuilder(seriesName)
            .WithLibraryId(1)
            .WithMetadata(new SeriesMetadataBuilder()
                .WithGenre(_genreLookup["Action"])
                .Build())
            .Build();
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        var metadataSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
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

        _context.MetadataSettings.Update(metadataSettings);
        await _context.SaveChangesAsync();


        await _externalMetadataService.WriteExternalMetadataToSeries(new ExternalSeriesDetailDto()
        {
            Name = seriesName,
        }, 1);

        // Repull Series and validate what is overwritten
        var postSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1, SeriesIncludes.Metadata);
        Assert.NotNull(postSeries);
        Assert.Equal(
            new[] {"Action"}.OrderBy(s => s),
            postSeries.Metadata.Genres.Select(g => g.Title).OrderBy(s => s)
        );
    }

    #endregion



    protected override async Task ResetDb()
    {
       _context.Series.RemoveRange(_context.Series);
       _context.AppUser.RemoveRange(_context.AppUser);
       _context.Genre.RemoveRange(_context.Genre);
       _context.Tag.RemoveRange(_context.Tag);
       _context.Person.RemoveRange(_context.Person);

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

       _context.AppUser.Add(new AppUserBuilder("Joe", "Joe")
           .WithRole(PolicyConstants.AdminRole)
           .WithLibrary(await _context.Library.FirstAsync(l => l.Id == 1))
           .Build());

       // Create a bunch of Genres for this test and store their string in _genreLookup
       _genreLookup.Clear();
       var g1 = new GenreBuilder("Action").Build();
       var g2 = new GenreBuilder("Ecchi").Build();
       _context.Genre.Add(g1);
       _context.Genre.Add(g2);
       _genreLookup.Add("Action", g1);
       _genreLookup.Add("Ecchi", g2);

       _tagLookup.Clear();
       var t1 = new TagBuilder("H").Build();
       var t2 = new TagBuilder("Boxing").Build();
       _context.Tag.Add(t1);
       _context.Tag.Add(t2);
       _tagLookup.Add("H", t1);
       _tagLookup.Add("Boxing", t2);

       _personLookup.Clear();
       var p1 = new PersonBuilder("Johnny Twowheeler").Build();
       var p2 = new PersonBuilder("Boxing").Build();
       _context.Person.Add(p1);
       _context.Person.Add(p2);
       _personLookup.Add("Johnny Twowheeler", p1);
       _personLookup.Add("Batman Robin", p2);

       await _context.SaveChangesAsync();
    }

    private static SeriesStaffDto CreateStaff(string first, string last, string role)
    {
        return new SeriesStaffDto() {Name = $"{first} {last}", Role = role, Url = "", FirstName = first, LastName = last};
    }

    private static SeriesCharacter CreateCharacter(string first, string last, CharacterRole role)
    {
        return new SeriesCharacter() {Name = $"{first} {last}", Description = "", Url = "", ImageUrl = "", Role = role};
    }
}
