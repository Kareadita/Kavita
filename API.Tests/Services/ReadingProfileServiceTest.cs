using System.Linq;
using System.Threading.Tasks;
using API.Data.Repositories;
using API.DTOs;
using API.Entities;
using API.Entities.Enums;
using API.Helpers.Builders;
using API.Services;
using API.Tests.Helpers;
using Kavita.Common;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;

public class ReadingProfileServiceTest: AbstractDbTest
{

    /// <summary>
    /// Does not add a default reading profile
    /// </summary>
    /// <returns></returns>
    public async Task<(ReadingProfileService, AppUser, Library, Series)> Setup()
    {
        var user = new AppUserBuilder("amelia", "amelia@localhost").Build();
        Context.AppUser.Add(user);
        await UnitOfWork.CommitAsync();

        var series = new SeriesBuilder("Spice and Wolf").Build();

        var library = new LibraryBuilder("Manga")
            .WithSeries(series)
            .Build();

        user.Libraries.Add(library);
        await UnitOfWork.CommitAsync();

        var rps = new ReadingProfileService(UnitOfWork, Substitute.For<ILocalizationService>(), Mapper);
        user = await UnitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.UserPreferences);

        return (rps, user, library, series);
    }

    [Fact]
    public async Task ImplicitProfileFirst()
    {
        await ResetDb();
        var (rps, user, library, series) = await Setup();

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
        await UnitOfWork.CommitAsync();

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
        await ResetDb();
        var (rps, user, _, _) = await Setup();

        var profile = new AppUserReadingProfileBuilder(user.Id)
            .WithKind(ReadingProfileKind.Default)
            .Build();
        Context.AppUserReadingProfiles.Add(profile);
        await UnitOfWork.CommitAsync();

        await Assert.ThrowsAsync<KavitaException>(async () =>
        {
            await rps.DeleteReadingProfile(user.Id, profile.Id);
        });

        var profile2 = new AppUserReadingProfileBuilder(user.Id).Build();
        Context.AppUserReadingProfiles.Add(profile2);
        await UnitOfWork.CommitAsync();

        await rps.DeleteReadingProfile(user.Id, profile2.Id);
        await UnitOfWork.CommitAsync();

        var allProfiles = await Context.AppUserReadingProfiles.ToListAsync();
        Assert.Single(allProfiles);
    }

    [Fact]
    public async Task CreateImplicitSeriesReadingProfile()
    {
        await ResetDb();
        var (rps, user, _, series) = await Setup();

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
        await ResetDb();
        var (rps, user, _, series) = await Setup();

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

        var implicitCount = await Context.AppUserReadingProfiles
            .Where(p => p.Kind == ReadingProfileKind.Implicit)
            .CountAsync();
        Assert.Equal(1, implicitCount);
    }

    [Fact]
    public async Task GetCorrectProfile()
    {
        await ResetDb();
        var (rps, user, lib, series) = await Setup();

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
        Context.AppUserReadingProfiles.Add(profile);
        Context.AppUserReadingProfiles.Add(profile2);
        Context.AppUserReadingProfiles.Add(profile3);

        var series2 = new SeriesBuilder("Rainbows After Storms").Build();
        lib.Series.Add(series2);

        var lib2 = new LibraryBuilder("Manga2").Build();
        var series3 = new SeriesBuilder("A Tropical Fish Yearns for Snow").Build();
        lib2.Series.Add(series3);

        user.Libraries.Add(lib2);
        await UnitOfWork.CommitAsync();

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
        await ResetDb();
        var (rps, user, lib, series) = await Setup();

        var profile1 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Profile 1")
            .Build();

        var profile2 = new AppUserReadingProfileBuilder(user.Id)
            .WithName("Profile 2")
            .Build();

        Context.AppUserReadingProfiles.Add(profile1);
        Context.AppUserReadingProfiles.Add(profile2);
        await UnitOfWork.CommitAsync();

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
        await ResetDb();
        var (rps, user, lib, series) = await Setup();

        var profile1 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Profile 1")
            .Build();

        Context.AppUserReadingProfiles.Add(profile1);
        await UnitOfWork.CommitAsync();

        await rps.ClearSeriesProfile(user.Id, series.Id);
        var profiles = await UnitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(user.Id);
        Assert.DoesNotContain(profiles, rp => rp.SeriesIds.Contains(series.Id));

    }

    [Fact]
    public async Task BulkAddReadingProfiles()
    {
        await ResetDb();
        var (rps, user, lib, series) = await Setup();

        for (var i = 0; i < 10; i++)
        {
            var generatedSeries = new SeriesBuilder($"Generated Series #{i}").Build();
            lib.Series.Add(generatedSeries);
        }

        var profile = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Profile")
            .Build();
        Context.AppUserReadingProfiles.Add(profile);

        var profile2 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Profile2")
            .Build();
        Context.AppUserReadingProfiles.Add(profile2);

        await UnitOfWork.CommitAsync();

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
        await ResetDb();
        var (rps, user, lib, series) = await Setup();

        var implicitProfile = Mapper.Map<UserReadingProfileDto>(new AppUserReadingProfileBuilder(user.Id)
            .Build());

        var profile = new AppUserReadingProfileBuilder(user.Id)
            .WithName("Profile 1")
            .Build();
        Context.AppUserReadingProfiles.Add(profile);

        for (var i = 0; i < 10; i++)
        {
            var generatedSeries = new SeriesBuilder($"Generated Series #{i}").Build();
            lib.Series.Add(generatedSeries);
        }
        await UnitOfWork.CommitAsync();

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

        var implicitCount = await Context.AppUserReadingProfiles
            .Where(p => p.Kind == ReadingProfileKind.Implicit)
            .CountAsync();
        Assert.Equal(0, implicitCount);
    }

    [Fact]
    public async Task AddDeletesImplicit()
    {
        await ResetDb();
        var (rps, user, lib, series) = await Setup();

        var implicitProfile = Mapper.Map<UserReadingProfileDto>(new AppUserReadingProfileBuilder(user.Id)
            .WithKind(ReadingProfileKind.Implicit)
            .Build());

        var profile = new AppUserReadingProfileBuilder(user.Id)
            .WithName("Profile 1")
            .Build();
        Context.AppUserReadingProfiles.Add(profile);
        await UnitOfWork.CommitAsync();

        await rps.UpdateImplicitReadingProfile(user.Id, series.Id, implicitProfile);

        var seriesProfile = await rps.GetReadingProfileDtoForSeries(user.Id, series.Id);
        Assert.NotNull(seriesProfile);
        Assert.Equal(ReadingProfileKind.Implicit, seriesProfile.Kind);

        await rps.AddProfileToSeries(user.Id, profile.Id, series.Id);

        seriesProfile = await rps.GetReadingProfileDtoForSeries(user.Id, series.Id);
        Assert.NotNull(seriesProfile);
        Assert.Equal(ReadingProfileKind.User, seriesProfile.Kind);

        var implicitCount = await Context.AppUserReadingProfiles
            .Where(p => p.Kind == ReadingProfileKind.Implicit)
            .CountAsync();
        Assert.Equal(0, implicitCount);
    }

    [Fact]
    public async Task CreateReadingProfile()
    {
        await ResetDb();
        var (rps, user, lib, series) = await Setup();

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

        var allProfiles = Context.AppUserReadingProfiles.ToList();
        Assert.Equal(2, allProfiles.Count);
    }

    [Fact]
    public async Task ClearSeriesProfile_RemovesImplicitAndUnlinksExplicit()
    {
        await ResetDb();
        var (rps, user, _, series) = await Setup();

        var implicitProfile = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithKind(ReadingProfileKind.Implicit)
            .WithName("Implicit Profile")
            .Build();

        var explicitProfile = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Explicit Profile")
            .Build();

        Context.AppUserReadingProfiles.Add(implicitProfile);
        Context.AppUserReadingProfiles.Add(explicitProfile);
        await UnitOfWork.CommitAsync();

        var allBefore = await UnitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(user.Id);
        Assert.Equal(2, allBefore.Count(rp => rp.SeriesIds.Contains(series.Id)));

        await rps.ClearSeriesProfile(user.Id, series.Id);

        var remainingProfiles = await Context.AppUserReadingProfiles.ToListAsync();
        Assert.Single(remainingProfiles);
        Assert.Equal("Explicit Profile", remainingProfiles[0].Name);
        Assert.Empty(remainingProfiles[0].SeriesIds);

        var profilesForSeries = await UnitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(user.Id);
        Assert.DoesNotContain(profilesForSeries, rp => rp.SeriesIds.Contains(series.Id));
    }

    [Fact]
    public async Task AddProfileToLibrary_AddsAndOverridesExisting()
    {
        await ResetDb();
        var (rps, user, lib, _) = await Setup();

        var profile = new AppUserReadingProfileBuilder(user.Id)
            .WithName("Library Profile")
            .Build();
        Context.AppUserReadingProfiles.Add(profile);
        await UnitOfWork.CommitAsync();

        await rps.AddProfileToLibrary(user.Id, profile.Id, lib.Id);
        await UnitOfWork.CommitAsync();

        var linkedProfile = (await UnitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(user.Id))
            .FirstOrDefault(rp => rp.LibraryIds.Contains(lib.Id));
        Assert.NotNull(linkedProfile);
        Assert.Equal(profile.Id, linkedProfile.Id);

        var newProfile = new AppUserReadingProfileBuilder(user.Id)
            .WithName("New Profile")
            .Build();
        Context.AppUserReadingProfiles.Add(newProfile);
        await UnitOfWork.CommitAsync();

        await rps.AddProfileToLibrary(user.Id, newProfile.Id, lib.Id);
        await UnitOfWork.CommitAsync();

        linkedProfile = (await UnitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(user.Id))
            .FirstOrDefault(rp => rp.LibraryIds.Contains(lib.Id));
        Assert.NotNull(linkedProfile);
        Assert.Equal(newProfile.Id, linkedProfile.Id);
    }

    [Fact]
    public async Task ClearLibraryProfile_RemovesImplicitOrUnlinksExplicit()
    {
        await ResetDb();
        var (rps, user, lib, _) = await Setup();

        var implicitProfile = new AppUserReadingProfileBuilder(user.Id)
            .WithKind(ReadingProfileKind.Implicit)
            .WithLibrary(lib)
            .Build();
        Context.AppUserReadingProfiles.Add(implicitProfile);
        await UnitOfWork.CommitAsync();

        await rps.ClearLibraryProfile(user.Id, lib.Id);
        var profile = (await UnitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(user.Id))
            .FirstOrDefault(rp => rp.LibraryIds.Contains(lib.Id));
        Assert.Null(profile);

        var explicitProfile = new AppUserReadingProfileBuilder(user.Id)
            .WithLibrary(lib)
            .Build();
        Context.AppUserReadingProfiles.Add(explicitProfile);
        await UnitOfWork.CommitAsync();

        await rps.ClearLibraryProfile(user.Id, lib.Id);
        profile = (await UnitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(user.Id))
            .FirstOrDefault(rp => rp.LibraryIds.Contains(lib.Id));
        Assert.Null(profile);

        var stillExists = await Context.AppUserReadingProfiles.FindAsync(explicitProfile.Id);
        Assert.NotNull(stillExists);
    }

    /// <summary>
    /// As response to #3793, I'm not sure if we want to keep this. It's not the most nice. But I think the idea of this test
    /// is worth having.
    /// </summary>
    [Fact]
    public void UpdateFields_UpdatesAll()
    {
        // Repeat to ensure booleans are flipped and actually tested
        for (int i = 0; i < 10; i++)
        {
            var profile = new AppUserReadingProfile();
            var dto = new UserReadingProfileDto();

            RandfHelper.SetRandomValues(profile);
            RandfHelper.SetRandomValues(dto);

            ReadingProfileService.UpdateReaderProfileFields(profile, dto);

            var newDto = Mapper.Map<UserReadingProfileDto>(profile);

            Assert.True(RandfHelper.AreSimpleFieldsEqual(dto, newDto,
                ["<Id>k__BackingField", "<UserId>k__BackingField"]));
        }
    }



    protected override async Task ResetDb()
    {
        Context.AppUserReadingProfiles.RemoveRange(Context.AppUserReadingProfiles);
        await UnitOfWork.CommitAsync();
    }
}
