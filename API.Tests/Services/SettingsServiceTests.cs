using System.Collections.Generic;
using System.IO.Abstractions;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.KavitaPlus.Metadata;
using API.Entities;
using API.Entities.Enums;
using API.Entities.MetadataMatching;
using API.Services;
using API.Services.Tasks.Scanner;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;

public class SettingsServiceTests
{
    private readonly ISettingsService _settingsService;
    private readonly IUnitOfWork _mockUnitOfWork;

    public SettingsServiceTests()
    {
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new FileSystem());

        _mockUnitOfWork = Substitute.For<IUnitOfWork>();
        _settingsService = new SettingsService(_mockUnitOfWork, ds,
            Substitute.For<ILibraryWatcher>(), Substitute.For<ITaskScheduler>(),
            Substitute.For<ILogger<SettingsService>>());
    }

    #region UpdateMetadataSettings

    [Fact]
    public async Task UpdateMetadataSettings_ShouldUpdateExistingSettings()
    {
        // Arrange
        var existingSettings = new MetadataSettings
        {
            Id = 1,
            Enabled = false,
            EnableSummary = false,
            EnableLocalizedName = false,
            EnablePublicationStatus = false,
            EnableRelationships = false,
            EnablePeople = false,
            EnableStartDate = false,
            EnableGenres = false,
            EnableTags = false,
            FirstLastPeopleNaming = false,
            EnableCoverImage = false,
            AgeRatingMappings = new Dictionary<string, AgeRating>(),
            Blacklist = [],
            Whitelist = [],
            Overrides = [],
            PersonRoles = [],
            FieldMappings = []
        };

        var settingsRepo = Substitute.For<ISettingsRepository>();
        settingsRepo.GetMetadataSettings().Returns(Task.FromResult(existingSettings));
        settingsRepo.GetMetadataSettingDto().Returns(Task.FromResult(new MetadataSettingsDto()));
        _mockUnitOfWork.SettingsRepository.Returns(settingsRepo);

        var updateDto = new MetadataSettingsDto
        {
            Enabled = true,
            EnableSummary = true,
            EnableLocalizedName = true,
            EnablePublicationStatus = true,
            EnableRelationships = true,
            EnablePeople = true,
            EnableStartDate = true,
            EnableGenres = true,
            EnableTags = true,
            FirstLastPeopleNaming = true,
            EnableCoverImage = true,
            AgeRatingMappings = new Dictionary<string, AgeRating> { { "Adult", AgeRating.R18Plus } },
            Blacklist = ["blacklisted-tag"],
            Whitelist = ["whitelisted-tag"],
            Overrides = [MetadataSettingField.Summary],
            PersonRoles = [PersonRole.Writer],
            FieldMappings =
            [
                new MetadataFieldMappingDto
                {
                    SourceType = MetadataFieldType.Genre,
                    DestinationType = MetadataFieldType.Tag,
                    SourceValue = "Action",
                    DestinationValue = "Fight",
                    ExcludeFromSource = true
                }
            ]
        };

        // Act
        await _settingsService.UpdateMetadataSettings(updateDto);

        // Assert
        await _mockUnitOfWork.Received(1).CommitAsync();

        // Verify properties were updated
        Assert.True(existingSettings.Enabled);
        Assert.True(existingSettings.EnableSummary);
        Assert.True(existingSettings.EnableLocalizedName);
        Assert.True(existingSettings.EnablePublicationStatus);
        Assert.True(existingSettings.EnableRelationships);
        Assert.True(existingSettings.EnablePeople);
        Assert.True(existingSettings.EnableStartDate);
        Assert.True(existingSettings.EnableGenres);
        Assert.True(existingSettings.EnableTags);
        Assert.True(existingSettings.FirstLastPeopleNaming);
        Assert.True(existingSettings.EnableCoverImage);

        // Verify collections were updated
        Assert.Single(existingSettings.AgeRatingMappings);
        Assert.Equal(AgeRating.R18Plus, existingSettings.AgeRatingMappings["Adult"]);

        Assert.Single(existingSettings.Blacklist);
        Assert.Equal("blacklisted-tag", existingSettings.Blacklist[0]);

        Assert.Single(existingSettings.Whitelist);
        Assert.Equal("whitelisted-tag", existingSettings.Whitelist[0]);

        Assert.Single(existingSettings.Overrides);
        Assert.Equal(MetadataSettingField.Summary, existingSettings.Overrides[0]);

        Assert.Single(existingSettings.PersonRoles);
        Assert.Equal(PersonRole.Writer, existingSettings.PersonRoles[0]);

        Assert.Single(existingSettings.FieldMappings);
        Assert.Equal(MetadataFieldType.Genre, existingSettings.FieldMappings[0].SourceType);
        Assert.Equal(MetadataFieldType.Tag, existingSettings.FieldMappings[0].DestinationType);
        Assert.Equal("Action", existingSettings.FieldMappings[0].SourceValue);
        Assert.Equal("Fight", existingSettings.FieldMappings[0].DestinationValue);
        Assert.True(existingSettings.FieldMappings[0].ExcludeFromSource);
    }

    [Fact]
    public async Task UpdateMetadataSettings_WithNullCollections_ShouldUseEmptyCollections()
    {
        // Arrange
        var existingSettings = new MetadataSettings
        {
            Id = 1,
            FieldMappings = [new MetadataFieldMapping {Id = 1, SourceValue = "OldValue"}]
        };

        var settingsRepo = Substitute.For<ISettingsRepository>();
        settingsRepo.GetMetadataSettings().Returns(Task.FromResult(existingSettings));
        settingsRepo.GetMetadataSettingDto().Returns(Task.FromResult(new MetadataSettingsDto()));
        _mockUnitOfWork.SettingsRepository.Returns(settingsRepo);

        var updateDto = new MetadataSettingsDto
        {
            AgeRatingMappings = null,
            Blacklist = null,
            Whitelist = null,
            Overrides = null,
            PersonRoles = null,
            FieldMappings = null
        };

        // Act
        await _settingsService.UpdateMetadataSettings(updateDto);

        // Assert
        await _mockUnitOfWork.Received(1).CommitAsync();

        Assert.Empty(existingSettings.AgeRatingMappings);
        Assert.Empty(existingSettings.Blacklist);
        Assert.Empty(existingSettings.Whitelist);
        Assert.Empty(existingSettings.Overrides);
        Assert.Empty(existingSettings.PersonRoles);

        // Verify existing field mappings were cleared
        settingsRepo.Received(1).RemoveRange(Arg.Any<List<MetadataFieldMapping>>());
        Assert.Empty(existingSettings.FieldMappings);
    }

    [Fact]
    public async Task UpdateMetadataSettings_WithFieldMappings_ShouldReplaceExistingMappings()
    {
        // Arrange
        var existingSettings = new MetadataSettings
        {
            Id = 1,
            FieldMappings =
            [
                new MetadataFieldMapping
                {
                    Id = 1,
                    SourceType = MetadataFieldType.Genre,
                    DestinationType = MetadataFieldType.Genre,
                    SourceValue = "OldValue",
                    DestinationValue = "OldDestination",
                    ExcludeFromSource = false
                }
            ]
        };

        var settingsRepo = Substitute.For<ISettingsRepository>();
        settingsRepo.GetMetadataSettings().Returns(Task.FromResult(existingSettings));
        settingsRepo.GetMetadataSettingDto().Returns(Task.FromResult(new MetadataSettingsDto()));
        _mockUnitOfWork.SettingsRepository.Returns(settingsRepo);

        var updateDto = new MetadataSettingsDto
        {
            FieldMappings =
            [
                new MetadataFieldMappingDto
                {
                    SourceType = MetadataFieldType.Tag,
                    DestinationType = MetadataFieldType.Genre,
                    SourceValue = "NewValue",
                    DestinationValue = "NewDestination",
                    ExcludeFromSource = true
                },

                new MetadataFieldMappingDto
                {
                    SourceType = MetadataFieldType.Tag,
                    DestinationType = MetadataFieldType.Tag,
                    SourceValue = "AnotherValue",
                    DestinationValue = "AnotherDestination",
                    ExcludeFromSource = false
                }
            ]
        };

        // Act
        await _settingsService.UpdateMetadataSettings(updateDto);

        // Assert
        await _mockUnitOfWork.Received(1).CommitAsync();

        // Verify existing field mappings were cleared and new ones added
        settingsRepo.Received(1).RemoveRange(Arg.Any<List<MetadataFieldMapping>>());
        Assert.Equal(2, existingSettings.FieldMappings.Count);

        // Verify first mapping
        Assert.Equal(MetadataFieldType.Tag, existingSettings.FieldMappings[0].SourceType);
        Assert.Equal(MetadataFieldType.Genre, existingSettings.FieldMappings[0].DestinationType);
        Assert.Equal("NewValue", existingSettings.FieldMappings[0].SourceValue);
        Assert.Equal("NewDestination", existingSettings.FieldMappings[0].DestinationValue);
        Assert.True(existingSettings.FieldMappings[0].ExcludeFromSource);

        // Verify second mapping
        Assert.Equal(MetadataFieldType.Tag, existingSettings.FieldMappings[1].SourceType);
        Assert.Equal(MetadataFieldType.Tag, existingSettings.FieldMappings[1].DestinationType);
        Assert.Equal("AnotherValue", existingSettings.FieldMappings[1].SourceValue);
        Assert.Equal("AnotherDestination", existingSettings.FieldMappings[1].DestinationValue);
        Assert.False(existingSettings.FieldMappings[1].ExcludeFromSource);
    }

    [Fact]
    public async Task UpdateMetadataSettings_WithBlacklistWhitelist_ShouldNormalizeAndDeduplicateEntries()
    {
        // Arrange
        var existingSettings = new MetadataSettings
        {
            Id = 1,
            Blacklist = [],
            Whitelist = []
        };

        // We need to mock the repository and provide a custom implementation for ToNormalized
        var settingsRepo = Substitute.For<ISettingsRepository>();
        settingsRepo.GetMetadataSettings().Returns(Task.FromResult(existingSettings));
        settingsRepo.GetMetadataSettingDto().Returns(Task.FromResult(new MetadataSettingsDto()));
        _mockUnitOfWork.SettingsRepository.Returns(settingsRepo);

        var updateDto = new MetadataSettingsDto
        {
            // Include duplicates with different casing and whitespace
            Blacklist = ["tag1", "Tag1", " tag2 ", "", "  ", "tag3"],
            Whitelist = ["allowed1", "Allowed1", " allowed2 ", "", "allowed3"]
        };

        // Act
        await _settingsService.UpdateMetadataSettings(updateDto);

        // Assert
        await _mockUnitOfWork.Received(1).CommitAsync();

        Assert.Equal(3, existingSettings.Blacklist.Count);
        Assert.Equal(3, existingSettings.Whitelist.Count);
    }

    #endregion
}
