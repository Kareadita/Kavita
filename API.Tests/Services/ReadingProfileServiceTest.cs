using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.Entities;
using API.Entities.Enums;
using API.Helpers.Builders;
using API.Services;
using API.Tests.Helpers;
using AutoMapper;
using Kavita.Common;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services;

public class ReadingProfileServiceTest(ITestOutputHelper outputHelper): AbstractDbTest(outputHelper)
{

    /// <summary>
    /// Does not add a default reading profile
    /// </summary>
    /// <returns></returns>
    public async Task<(ReadingProfileService, AppUser, Library, Series)> Setup(IUnitOfWork unitOfWork, DataContext context, IMapper mapper)
    {
        var user = new AppUserBuilder("amelia", "amelia@localhost").Build();
        context.AppUser.Add(user);
        await unitOfWork.CommitAsync();

        var series = new SeriesBuilder("Spice and Wolf").Build();

        var library = new LibraryBuilder("Manga")
            .WithSeries(series)
            .Build();

        user.Libraries.Add(library);
        await unitOfWork.CommitAsync();

        var rps = new ReadingProfileService(unitOfWork, Substitute.For<ILocalizationService>(), mapper);
        user = await unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.UserPreferences);

        return (rps, user, library, series);
    }

    [Fact]
    public async Task ImplicitProfileFirst()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, library, series) = await Setup(unitOfWork, context, mapper);

        var profile = new AppUserReadingProfileBuilder(user.Id)
            .WithKind(ReadingProfileKind.Implicit)
            .WithSeries(series)
            .WithName("Implicit Profile")
            .Build();

        var profile2 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Non-implicit Profile")
            .Build();

        user.ReadingProfiles.Add(profile);
        user.ReadingProfiles.Add(profile2);
        await unitOfWork.CommitAsync();

        var seriesProfile = await rps.GetReadingProfileDtoForSeries(user.Id, series.Id);
        Assert.NotNull(seriesProfile);
        Assert.Equal("Implicit Profile", seriesProfile.Name);

        // Find parent
        seriesProfile = await rps.GetReadingProfileDtoForSeries(user.Id, series.Id, true);
        Assert.NotNull(seriesProfile);
        Assert.Equal("Non-implicit Profile", seriesProfile.Name);
    }

    [Fact]
    public async Task CantDeleteDefaultReadingProfile()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, _) = await Setup(unitOfWork, context, mapper);

        var profile = new AppUserReadingProfileBuilder(user.Id)
            .WithKind(ReadingProfileKind.Default)
            .Build();
        context.AppUserReadingProfiles.Add(profile);
        await unitOfWork.CommitAsync();

        await Assert.ThrowsAsync<KavitaException>(async () =>
        {
            await rps.DeleteReadingProfile(user.Id, profile.Id);
        });

        var profile2 = new AppUserReadingProfileBuilder(user.Id).Build();
        context.AppUserReadingProfiles.Add(profile2);
        await unitOfWork.CommitAsync();

        await rps.DeleteReadingProfile(user.Id, profile2.Id);
        await unitOfWork.CommitAsync();

        var allProfiles = await context.AppUserReadingProfiles.ToListAsync();
        Assert.Single(allProfiles);
    }

    [Fact]
    public async Task CreateImplicitSeriesReadingProfile()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

        var dto = new UserReadingProfileDto
        {
            ReaderMode = ReaderMode.Webtoon,
            ScalingOption = ScalingOption.FitToHeight,
            WidthOverride = 53,
        };

        await rps.UpdateImplicitReadingProfile(user.Id, series.Id, dto);

        var profile = await rps.GetReadingProfileForSeries(user.Id, series.Id);
        Assert.NotNull(profile);
        Assert.Contains(profile.SeriesIds, s => s == series.Id);
        Assert.Equal(ReadingProfileKind.Implicit, profile.Kind);
    }

    [Fact]
    public async Task UpdateImplicitReadingProfile_DoesNotCreateNew()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

        var dto = new UserReadingProfileDto
        {
            ReaderMode = ReaderMode.Webtoon,
            ScalingOption = ScalingOption.FitToHeight,
            WidthOverride = 53,
        };

        await rps.UpdateImplicitReadingProfile(user.Id, series.Id, dto);

        var profile =  await rps.GetReadingProfileForSeries(user.Id, series.Id);
        Assert.NotNull(profile);
        Assert.Contains(profile.SeriesIds, s => s == series.Id);
        Assert.Equal(ReadingProfileKind.Implicit, profile.Kind);

        dto = new UserReadingProfileDto
        {
            ReaderMode = ReaderMode.LeftRight,
        };

        await rps.UpdateImplicitReadingProfile(user.Id, series.Id, dto);
        profile =  await rps.GetReadingProfileForSeries(user.Id, series.Id);
        Assert.NotNull(profile);
        Assert.Contains(profile.SeriesIds, s => s == series.Id);
        Assert.Equal(ReadingProfileKind.Implicit, profile.Kind);
        Assert.Equal(ReaderMode.LeftRight, profile.ReaderMode);

        var implicitCount = await context.AppUserReadingProfiles
            .Where(p => p.Kind == ReadingProfileKind.Implicit)
            .CountAsync();
        Assert.Equal(1, implicitCount);
    }

    [Fact]
    public async Task GetCorrectProfile()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, lib, series) = await Setup(unitOfWork, context, mapper);

        var profile = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Series Specific")
            .Build();
        var profile2 = new AppUserReadingProfileBuilder(user.Id)
            .WithLibrary(lib)
            .WithName("Library Specific")
            .Build();
        var profile3 = new AppUserReadingProfileBuilder(user.Id)
            .WithKind(ReadingProfileKind.Default)
            .WithName("Global")
            .Build();
        context.AppUserReadingProfiles.Add(profile);
        context.AppUserReadingProfiles.Add(profile2);
        context.AppUserReadingProfiles.Add(profile3);

        var series2 = new SeriesBuilder("Rainbows After Storms").Build();
        lib.Series.Add(series2);

        var lib2 = new LibraryBuilder("Manga2").Build();
        var series3 = new SeriesBuilder("A Tropical Fish Yearns for Snow").Build();
        lib2.Series.Add(series3);

        user.Libraries.Add(lib2);
        await unitOfWork.CommitAsync();

        var p = await rps.GetReadingProfileDtoForSeries(user.Id, series.Id);
        Assert.NotNull(p);
        Assert.Equal("Series Specific", p.Name);

        p = await rps.GetReadingProfileDtoForSeries(user.Id, series2.Id);
        Assert.NotNull(p);
        Assert.Equal("Library Specific", p.Name);

        p = await rps.GetReadingProfileDtoForSeries(user.Id, series3.Id);
        Assert.NotNull(p);
        Assert.Equal("Global", p.Name);
    }

    [Fact]
    public async Task ReplaceReadingProfile()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, lib, series) = await Setup(unitOfWork, context, mapper);

        var profile1 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Profile 1")
            .Build();

        var profile2 = new AppUserReadingProfileBuilder(user.Id)
            .WithName("Profile 2")
            .Build();

        context.AppUserReadingProfiles.Add(profile1);
        context.AppUserReadingProfiles.Add(profile2);
        await unitOfWork.CommitAsync();

        var profile = await rps.GetReadingProfileDtoForSeries(user.Id, series.Id);
        Assert.NotNull(profile);
        Assert.Equal("Profile 1", profile.Name);

        await rps.AddProfileToSeries(user.Id, profile2.Id, series.Id);
        profile = await rps.GetReadingProfileDtoForSeries(user.Id, series.Id);
        Assert.NotNull(profile);
        Assert.Equal("Profile 2", profile.Name);
    }

    [Fact]
    public async Task DeleteReadingProfile()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, lib, series) = await Setup(unitOfWork, context, mapper);

        var profile1 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Profile 1")
            .Build();

        context.AppUserReadingProfiles.Add(profile1);
        await unitOfWork.CommitAsync();

        await rps.ClearSeriesProfile(user.Id, series.Id);
        var profiles = await unitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(user.Id);
        Assert.DoesNotContain(profiles, rp => rp.SeriesIds.Contains(series.Id));

    }

    [Fact]
    public async Task BulkAddReadingProfiles()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, lib, series) = await Setup(unitOfWork, context, mapper);

        for (var i = 0; i < 10; i++)
        {
            var generatedSeries = new SeriesBuilder($"Generated Series #{i}").Build();
            lib.Series.Add(generatedSeries);
        }

        var profile = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Profile")
            .Build();
        context.AppUserReadingProfiles.Add(profile);

        var profile2 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Profile2")
            .Build();
        context.AppUserReadingProfiles.Add(profile2);

        await unitOfWork.CommitAsync();

        var someSeriesIds = lib.Series.Take(lib.Series.Count / 2).Select(s => s.Id).ToList();
        await rps.BulkAddProfileToSeries(user.Id, profile.Id, someSeriesIds);

        foreach (var id in someSeriesIds)
        {
            var foundProfile = await rps.GetReadingProfileDtoForSeries(user.Id, id);
            Assert.NotNull(foundProfile);
            Assert.Equal(profile.Id, foundProfile.Id);
        }

        var allIds = lib.Series.Select(s => s.Id).ToList();
        await rps.BulkAddProfileToSeries(user.Id, profile2.Id, allIds);

        foreach (var id in allIds)
        {
            var foundProfile = await rps.GetReadingProfileDtoForSeries(user.Id, id);
            Assert.NotNull(foundProfile);
            Assert.Equal(profile2.Id, foundProfile.Id);
        }


    }

    [Fact]
    public async Task BulkAssignDeletesImplicit()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, lib, series) = await Setup(unitOfWork, context, mapper);

        var implicitProfile = mapper.Map<UserReadingProfileDto>(new AppUserReadingProfileBuilder(user.Id)
            .Build());

        var profile = new AppUserReadingProfileBuilder(user.Id)
            .WithName("Profile 1")
            .Build();
        context.AppUserReadingProfiles.Add(profile);

        for (var i = 0; i < 10; i++)
        {
            var generatedSeries = new SeriesBuilder($"Generated Series #{i}").Build();
            lib.Series.Add(generatedSeries);
        }
        await unitOfWork.CommitAsync();

        var ids = lib.Series.Select(s => s.Id).ToList();

        foreach (var id in ids)
        {
            await rps.UpdateImplicitReadingProfile(user.Id, id, implicitProfile);
            var seriesProfile = await rps.GetReadingProfileDtoForSeries(user.Id, id);
            Assert.NotNull(seriesProfile);
            Assert.Equal(ReadingProfileKind.Implicit, seriesProfile.Kind);
        }

        await rps.BulkAddProfileToSeries(user.Id, profile.Id, ids);

        foreach (var id in ids)
        {
            var seriesProfile = await rps.GetReadingProfileDtoForSeries(user.Id, id);
            Assert.NotNull(seriesProfile);
            Assert.Equal(ReadingProfileKind.User, seriesProfile.Kind);
        }

        var implicitCount = await context.AppUserReadingProfiles
            .Where(p => p.Kind == ReadingProfileKind.Implicit)
            .CountAsync();
        Assert.Equal(0, implicitCount);
    }

    [Fact]
    public async Task AddDeletesImplicit()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, lib, series) = await Setup(unitOfWork, context, mapper);

        var implicitProfile = mapper.Map<UserReadingProfileDto>(new AppUserReadingProfileBuilder(user.Id)
            .WithKind(ReadingProfileKind.Implicit)
            .Build());

        var profile = new AppUserReadingProfileBuilder(user.Id)
            .WithName("Profile 1")
            .Build();
        context.AppUserReadingProfiles.Add(profile);
        await unitOfWork.CommitAsync();

        await rps.UpdateImplicitReadingProfile(user.Id, series.Id, implicitProfile);

        var seriesProfile = await rps.GetReadingProfileDtoForSeries(user.Id, series.Id);
        Assert.NotNull(seriesProfile);
        Assert.Equal(ReadingProfileKind.Implicit, seriesProfile.Kind);

        await rps.AddProfileToSeries(user.Id, profile.Id, series.Id);

        seriesProfile = await rps.GetReadingProfileDtoForSeries(user.Id, series.Id);
        Assert.NotNull(seriesProfile);
        Assert.Equal(ReadingProfileKind.User, seriesProfile.Kind);

        var implicitCount = await context.AppUserReadingProfiles
            .Where(p => p.Kind == ReadingProfileKind.Implicit)
            .CountAsync();
        Assert.Equal(0, implicitCount);
    }

    [Fact]
    public async Task CreateReadingProfile()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, lib, series) = await Setup(unitOfWork, context, mapper);

        var dto = new UserReadingProfileDto
        {
            Name = "Profile 1",
            ReaderMode = ReaderMode.LeftRight,
            EmulateBook = false,
        };

        await rps.CreateReadingProfile(user.Id, dto);

        var dto2 = new UserReadingProfileDto
        {
            Name = "Profile 2",
            ReaderMode = ReaderMode.LeftRight,
            EmulateBook = false,
        };

        await rps.CreateReadingProfile(user.Id, dto2);

        var dto3 = new UserReadingProfileDto
        {
            Name = "Profile 1", // Not unique name
            ReaderMode = ReaderMode.LeftRight,
            EmulateBook = false,
        };

        await Assert.ThrowsAsync<KavitaException>(async () =>
        {
            await rps.CreateReadingProfile(user.Id, dto3);
        });

        var allProfiles = context.AppUserReadingProfiles.ToList();
        Assert.Equal(2, allProfiles.Count);
    }

    [Fact]
    public async Task ClearSeriesProfile_RemovesImplicitAndUnlinksExplicit()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

        var implicitProfile = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithKind(ReadingProfileKind.Implicit)
            .WithName("Implicit Profile")
            .Build();

        var explicitProfile = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Explicit Profile")
            .Build();

        context.AppUserReadingProfiles.Add(implicitProfile);
        context.AppUserReadingProfiles.Add(explicitProfile);
        await unitOfWork.CommitAsync();

        var allBefore = await unitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(user.Id);
        Assert.Equal(2, allBefore.Count(rp => rp.SeriesIds.Contains(series.Id)));

        await rps.ClearSeriesProfile(user.Id, series.Id);

        var remainingProfiles = await context.AppUserReadingProfiles.ToListAsync();
        Assert.Single(remainingProfiles);
        Assert.Equal("Explicit Profile", remainingProfiles[0].Name);
        Assert.Empty(remainingProfiles[0].SeriesIds);

        var profilesForSeries = await unitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(user.Id);
        Assert.DoesNotContain(profilesForSeries, rp => rp.SeriesIds.Contains(series.Id));
    }

    [Fact]
    public async Task AddProfileToLibrary_AddsAndOverridesExisting()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, lib, _) = await Setup(unitOfWork, context, mapper);

        var profile = new AppUserReadingProfileBuilder(user.Id)
            .WithName("Library Profile")
            .Build();
        context.AppUserReadingProfiles.Add(profile);
        await unitOfWork.CommitAsync();

        await rps.AddProfileToLibrary(user.Id, profile.Id, lib.Id);
        await unitOfWork.CommitAsync();

        var linkedProfile = (await unitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(user.Id))
            .FirstOrDefault(rp => rp.LibraryIds.Contains(lib.Id));
        Assert.NotNull(linkedProfile);
        Assert.Equal(profile.Id, linkedProfile.Id);

        var newProfile = new AppUserReadingProfileBuilder(user.Id)
            .WithName("New Profile")
            .Build();
        context.AppUserReadingProfiles.Add(newProfile);
        await unitOfWork.CommitAsync();

        await rps.AddProfileToLibrary(user.Id, newProfile.Id, lib.Id);
        await unitOfWork.CommitAsync();

        linkedProfile = (await unitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(user.Id))
            .FirstOrDefault(rp => rp.LibraryIds.Contains(lib.Id));
        Assert.NotNull(linkedProfile);
        Assert.Equal(newProfile.Id, linkedProfile.Id);
    }

    [Fact]
    public async Task ClearLibraryProfile_RemovesImplicitOrUnlinksExplicit()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, lib, _) = await Setup(unitOfWork, context, mapper);

        var implicitProfile = new AppUserReadingProfileBuilder(user.Id)
            .WithKind(ReadingProfileKind.Implicit)
            .WithLibrary(lib)
            .Build();
        context.AppUserReadingProfiles.Add(implicitProfile);
        await unitOfWork.CommitAsync();

        await rps.ClearLibraryProfile(user.Id, lib.Id);
        var profile = (await unitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(user.Id))
            .FirstOrDefault(rp => rp.LibraryIds.Contains(lib.Id));
        Assert.Null(profile);

        var explicitProfile = new AppUserReadingProfileBuilder(user.Id)
            .WithLibrary(lib)
            .Build();
        context.AppUserReadingProfiles.Add(explicitProfile);
        await unitOfWork.CommitAsync();

        await rps.ClearLibraryProfile(user.Id, lib.Id);
        profile = (await unitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(user.Id))
            .FirstOrDefault(rp => rp.LibraryIds.Contains(lib.Id));
        Assert.Null(profile);

        var stillExists = await context.AppUserReadingProfiles.FindAsync(explicitProfile.Id);
        Assert.NotNull(stillExists);
    }

    /// <summary>
    /// As response to #3793, I'm not sure if we want to keep this. It's not the most nice. But I think the idea of this test
    /// is worth having.
    /// </summary>
    [Fact]
    public async Task UpdateFields_UpdatesAll()
    {
        var (_, _, mapper) = await CreateDatabase();

        // Repeat to ensure booleans are flipped and actually tested
        for (int i = 0; i < 10; i++)
        {
            var profile = new AppUserReadingProfile();
            var dto = new UserReadingProfileDto();

            RandfHelper.SetRandomValues(profile);
            RandfHelper.SetRandomValues(dto);

            ReadingProfileService.UpdateReaderProfileFields(profile, dto);

            var newDto = mapper.Map<UserReadingProfileDto>(profile);

            Assert.True(RandfHelper.AreSimpleFieldsEqual(dto, newDto,
                ["<Id>k__BackingField", "<UserId>k__BackingField"]));
        }
    }

}
