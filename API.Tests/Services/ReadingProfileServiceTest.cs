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
    private static async Task<(ReadingProfileService, AppUser, Library, Series)> Setup(IUnitOfWork unitOfWork, DataContext context, IMapper mapper)
    {
        var user = new AppUserBuilder("amelia", "amelia@localhost").Build();
        context.AppUser.Add(user);
        await unitOfWork.CommitAsync();

        var series = new SeriesBuilder("Spice and Wolf").Build();

        var library = new LibraryBuilder("Manga")
            .WithSeries(series)
            .Build();

        user.Libraries.Add(library);

        context.AppUserReadingProfiles.Add(new AppUserReadingProfileBuilder(user!.Id)
            .WithName("Global")
            .WithKind(ReadingProfileKind.Default)
            .Build());

        await unitOfWork.CommitAsync();

        var rps = new ReadingProfileService(unitOfWork, Substitute.For<ILocalizationService>(), mapper);
        user = await unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.UserPreferences);

        return (rps, user, library, series);
    }

    #region Pre-Device Tests - Must of course keep passing

    [Fact]
    public async Task ImplicitProfileFirst()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

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

        var seriesProfile = await rps.GetReadingProfileDtoForSeries(user.Id, series.LibraryId, series.Id, null);
        Assert.NotNull(seriesProfile);
        Assert.Equal("Implicit Profile", seriesProfile.Name);

        // Find parent
        seriesProfile = await rps.GetReadingProfileDtoForSeries(user.Id, series.LibraryId, series.Id, null, true);
        Assert.NotNull(seriesProfile);
        Assert.Equal("Non-implicit Profile", seriesProfile.Name);
    }

    [Fact]
    public async Task CantDeleteDefaultReadingProfile()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, _) = await Setup(unitOfWork, context, mapper);

        var profile = await context.AppUserReadingProfiles
            .FirstOrDefaultAsync(rp => rp.Kind == ReadingProfileKind.Default);

        Assert.NotNull(profile);

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

        await rps.UpdateImplicitReadingProfile(user.Id, series.LibraryId, series.Id, dto, null);

        var profile = await rps.GetReadingProfileForSeries(user.Id, series.LibraryId, series.Id, null);
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

        await rps.UpdateImplicitReadingProfile(user.Id, series.LibraryId, series.Id, dto, null);

        var profile =  await rps.GetReadingProfileForSeries(user.Id, series.LibraryId, series.Id, null);
        Assert.NotNull(profile);
        Assert.Contains(profile.SeriesIds, s => s == series.Id);
        Assert.Equal(ReadingProfileKind.Implicit, profile.Kind);

        dto = new UserReadingProfileDto
        {
            ReaderMode = ReaderMode.LeftRight,
        };

        await rps.UpdateImplicitReadingProfile(user.Id, series.LibraryId, series.Id, dto, null);
        profile =  await rps.GetReadingProfileForSeries(user.Id, series.LibraryId, series.Id, null);
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
        context.AppUserReadingProfiles.Add(profile);
        context.AppUserReadingProfiles.Add(profile2);

        var series2 = new SeriesBuilder("Rainbows After Storms").Build();
        lib.Series.Add(series2);

        var lib2 = new LibraryBuilder("Manga2").Build();
        var series3 = new SeriesBuilder("A Tropical Fish Yearns for Snow").Build();
        lib2.Series.Add(series3);

        user.Libraries.Add(lib2);
        await unitOfWork.CommitAsync();

        var p = await rps.GetReadingProfileDtoForSeries(user.Id, series.LibraryId, series.Id, null);
        Assert.NotNull(p);
        Assert.Equal("Series Specific", p.Name);

        p = await rps.GetReadingProfileDtoForSeries(user.Id, series2.LibraryId, series2.Id, null);
        Assert.NotNull(p);
        Assert.Equal("Library Specific", p.Name);

        p = await rps.GetReadingProfileDtoForSeries(user.Id, series3.LibraryId, series3.Id, null);
        Assert.NotNull(p);
        Assert.Equal("Global", p.Name);
    }

    [Fact]
    public async Task ReplaceReadingProfile()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

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

        var profile = await rps.GetReadingProfileDtoForSeries(user.Id, series.LibraryId, series.Id, null);
        Assert.NotNull(profile);
        Assert.Equal("Profile 1", profile.Name);

        await rps.SetSeriesProfiles(user.Id, [profile2.Id], series.Id);
        profile = await rps.GetReadingProfileDtoForSeries(user.Id, series.LibraryId, series.Id, null);
        Assert.NotNull(profile);
        Assert.Equal("Profile 2", profile.Name);
    }

    [Fact]
    public async Task DeleteReadingProfile()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

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

        var someSeriesIds = lib.Series.Take(lib.Series.Count / 2).Select(s => new {s.Id, s.LibraryId}).ToList();
        await rps.BulkSetSeriesProfiles(user.Id, [profile.Id], someSeriesIds.Select(x => x.Id).ToList());

        foreach (var x in someSeriesIds)
        {
            var foundProfile = await rps.GetReadingProfileDtoForSeries(user.Id, x.LibraryId, x.Id, null);
            Assert.NotNull(foundProfile);
            Assert.Equal(profile.Id, foundProfile.Id);
        }

        var allIds = lib.Series.Select(s => new {s.Id, s.LibraryId}).ToList();
        await rps.BulkSetSeriesProfiles(user.Id, [profile2.Id], allIds.Select(x => x.Id).ToList());

        foreach (var x in allIds)
        {
            var foundProfile = await rps.GetReadingProfileDtoForSeries(user.Id, x.LibraryId, x.Id, null);
            Assert.NotNull(foundProfile);
            Assert.Equal(profile2.Id, foundProfile.Id);
        }


    }

    [Fact]
    public async Task BulkAssignDeletesImplicit()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, lib, _) = await Setup(unitOfWork, context, mapper);

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

        var ids = lib.Series.Select(s => new {s.Id, s.LibraryId}).ToList();

        foreach (var x in ids)
        {
            await rps.UpdateImplicitReadingProfile(user.Id, x.LibraryId, x.Id, implicitProfile, null);
            var seriesProfile = await rps.GetReadingProfileDtoForSeries(user.Id, x.LibraryId, x.Id, null);
            Assert.NotNull(seriesProfile);
            Assert.Equal(ReadingProfileKind.Implicit, seriesProfile.Kind);
        }

        await rps.BulkSetSeriesProfiles(user.Id, [profile.Id], ids.Select(x => x.Id).ToList());

        foreach (var x in ids)
        {
            var seriesProfile = await rps.GetReadingProfileDtoForSeries(user.Id, x.LibraryId, x.Id, null);
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
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

        var implicitProfile = mapper.Map<UserReadingProfileDto>(new AppUserReadingProfileBuilder(user.Id)
            .WithKind(ReadingProfileKind.Implicit)
            .Build());

        var profile = new AppUserReadingProfileBuilder(user.Id)
            .WithName("Profile 1")
            .Build();
        context.AppUserReadingProfiles.Add(profile);
        await unitOfWork.CommitAsync();

        await rps.UpdateImplicitReadingProfile(user.Id, series.LibraryId, series.Id, implicitProfile, null);

        var seriesProfile = await rps.GetReadingProfileDtoForSeries(user.Id, series.LibraryId, series.Id, null);
        Assert.NotNull(seriesProfile);
        Assert.Equal(ReadingProfileKind.Implicit, seriesProfile.Kind);

        await rps.SetSeriesProfiles(user.Id, [profile.Id], series.Id);

        seriesProfile = await rps.GetReadingProfileDtoForSeries(user.Id, series.LibraryId, series.Id, null);
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
        var (rps, user, _, _) = await Setup(unitOfWork, context, mapper);

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
        Assert.Equal(3, allProfiles.Count);
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

        var remainingProfiles = await context.AppUserReadingProfiles
            .Where(p => p.Kind != ReadingProfileKind.Default)
            .ToListAsync();

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

        await rps.SetLibraryProfiles(user.Id, [profile.Id], lib.Id);
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

        await rps.SetLibraryProfiles(user.Id, [newProfile.Id], lib.Id);
        await unitOfWork.CommitAsync();

        linkedProfile = (await unitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(user.Id))
            .FirstOrDefault(rp => rp.LibraryIds.Contains(lib.Id));
        Assert.NotNull(linkedProfile);
        Assert.Equal(newProfile.Id, linkedProfile.Id);
    }

    #endregion

    #region Tests with devices - Get profile

    [Fact]
    public async Task GetCorrectProfile_WithMatchingDevice()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, lib, series) = await Setup(unitOfWork, context, mapper);

        const int deviceId = 1;

        var profileSeriesDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(deviceId)
            .WithName("Series Specific (Device)")
            .Build();
        var profileSeriesNoDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Series Specific (No Device)")
            .Build();
        var profileLibraryDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithLibrary(lib)
            .WithDeviceId(deviceId)
            .WithName("Library Specific (Device)")
            .Build();
        var profileLibraryNoDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithLibrary(lib)
            .WithName("Library Specific (No Device)")
            .Build();

        context.AppUserReadingProfiles.Add(profileSeriesDevice);
        context.AppUserReadingProfiles.Add(profileSeriesNoDevice);
        context.AppUserReadingProfiles.Add(profileLibraryDevice);
        context.AppUserReadingProfiles.Add(profileLibraryNoDevice);
        await unitOfWork.CommitAsync();

        // Should get series-specific profile with matching device
        var p = await rps.GetReadingProfileDtoForSeries(user.Id, series.LibraryId, series.Id, deviceId);
        Assert.NotNull(p);
        Assert.Equal("Series Specific (Device)", p.Name);
    }

    [Fact]
    public async Task GetCorrectProfile_WithNonMatchingDevice()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, lib, series) = await Setup(unitOfWork, context, mapper);

        const int deviceId = 1;
        const int otherDeviceId = 2;

        var profileSeriesWrongDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(otherDeviceId)
            .WithName("Series Specific (Wrong Device)")
            .Build();
        var profileSeriesNoDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Series Specific (No Device)")
            .Build();
        var profileLibraryDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithLibrary(lib)
            .WithDeviceId(deviceId)
            .WithName("Library Specific (Device)")
            .Build();

        context.AppUserReadingProfiles.Add(profileSeriesWrongDevice);
        context.AppUserReadingProfiles.Add(profileSeriesNoDevice);
        context.AppUserReadingProfiles.Add(profileLibraryDevice);
        await unitOfWork.CommitAsync();

        // Should skip series profile with wrong device, get series profile with no device
        var p = await rps.GetReadingProfileDtoForSeries(user.Id, series.LibraryId, series.Id, deviceId);
        Assert.NotNull(p);
        Assert.Equal("Series Specific (No Device)", p.Name);
    }

    [Fact]
    public async Task GetCorrectProfile_DeviceVsNoDevice_SeriesLevel()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

        const int deviceId = 1;

        var profileSeriesDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(deviceId)
            .WithName("Series (Device)")
            .Build();
        var profileSeriesNoDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Series (No Device)")
            .Build();

        context.AppUserReadingProfiles.Add(profileSeriesDevice);
        context.AppUserReadingProfiles.Add(profileSeriesNoDevice);
        await unitOfWork.CommitAsync();

        // With matching device, should prefer device-specific
        var p = await rps.GetReadingProfileDtoForSeries(user.Id, series.LibraryId, series.Id, deviceId);
        Assert.NotNull(p);
        Assert.Equal("Series (Device)", p.Name);

        // Without device specified, should get no-device profile
        p = await rps.GetReadingProfileDtoForSeries(user.Id, series.LibraryId, series.Id, null);
        Assert.NotNull(p);
        Assert.Equal("Series (No Device)", p.Name);
    }

    [Fact]
    public async Task GetCorrectProfile_DeviceVsNoDevice_LibraryLevel()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, lib, series) = await Setup(unitOfWork, context, mapper);

        const int deviceId = 1;

        var profileLibraryDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithLibrary(lib)
            .WithDeviceId(deviceId)
            .WithName("Library (Device)")
            .Build();
        var profileLibraryNoDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithLibrary(lib)
            .WithName("Library (No Device)")
            .Build();

        context.AppUserReadingProfiles.Add(profileLibraryDevice);
        context.AppUserReadingProfiles.Add(profileLibraryNoDevice);
        await unitOfWork.CommitAsync();

        // With matching device, should prefer device-specific
        var p = await rps.GetReadingProfileDtoForSeries(user.Id, series.LibraryId, series.Id, deviceId);
        Assert.NotNull(p);
        Assert.Equal("Library (Device)", p.Name);

        // Without device specified, should get no-device profile
        p = await rps.GetReadingProfileDtoForSeries(user.Id, series.LibraryId, series.Id, null);
        Assert.NotNull(p);
        Assert.Equal("Library (No Device)", p.Name);
    }

    [Fact]
    public async Task GetCorrectProfile_ImplicitWithDevice()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

        const int deviceId = 1;

        var profileImplicitDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(deviceId)
            .WithName("Implicit (Device)")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();
        var profileImplicitNoDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Implicit (No Device)")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();
        var profileSeriesDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(deviceId)
            .WithName("Series (Device)")
            .Build();

        context.AppUserReadingProfiles.Add(profileImplicitDevice);
        context.AppUserReadingProfiles.Add(profileImplicitNoDevice);
        context.AppUserReadingProfiles.Add(profileSeriesDevice);
        await unitOfWork.CommitAsync();

        // Implicit with device should win over everything else
        var p = await rps.GetReadingProfileDtoForSeries(user.Id, series.LibraryId, series.Id, deviceId);
        Assert.NotNull(p);
        Assert.Equal("Implicit (Device)", p.Name);

        // Without device, implicit no-device should win
        p = await rps.GetReadingProfileDtoForSeries(user.Id, series.LibraryId, series.Id, null);
        Assert.NotNull(p);
        Assert.Equal("Implicit (No Device)", p.Name);
    }

    [Fact]
    public async Task GetCorrectProfile_ComplexHierarchy_WithDevice()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, lib, series) = await Setup(unitOfWork, context, mapper);

        const int deviceId = 1;
        const int otherDeviceId = 2;

        var series2 = new SeriesBuilder("Rainbows After Storms").Build();
        lib.Series.Add(series2);

        var lib2 = new LibraryBuilder("Manga2").Build();
        var series3 = new SeriesBuilder("A Tropical Fish Yearns for Snow").Build();
        lib2.Series.Add(series3);
        user.Libraries.Add(lib2);

        // Series 1: has series-specific profile with device
        var profileSeries1Device = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(deviceId)
            .WithName("Series 1 (Device)")
            .Build();

        // Series 2: has library profile with device, and series profile with wrong device
        var profileSeries2WrongDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series2)
            .WithDeviceId(otherDeviceId)
            .WithName("Series 2 (Wrong Device)")
            .Build();
        var profileLibDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithLibrary(lib)
            .WithDeviceId(deviceId)
            .WithName("Library (Device)")
            .Build();

        // Series 3: has library profile with wrong device, should fall back to global
        var profileLib2WrongDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithLibrary(lib2)
            .WithDeviceId(otherDeviceId)
            .WithName("Library 2 (Wrong Device)")
            .Build();

        context.AppUserReadingProfiles.Add(profileSeries1Device);
        context.AppUserReadingProfiles.Add(profileSeries2WrongDevice);
        context.AppUserReadingProfiles.Add(profileLibDevice);
        context.AppUserReadingProfiles.Add(profileLib2WrongDevice);
        await unitOfWork.CommitAsync();

        // Series 1 should get series-specific with device
        var p = await rps.GetReadingProfileDtoForSeries(user.Id, series.LibraryId, series.Id, deviceId);
        Assert.NotNull(p);
        Assert.Equal("Series 1 (Device)", p.Name);

        // Series 2 should skip wrong device, get library with device
        p = await rps.GetReadingProfileDtoForSeries(user.Id, series2.LibraryId, series2.Id, deviceId);
        Assert.NotNull(p);
        Assert.Equal("Library (Device)", p.Name);

        // Series 3 should skip wrong device library profile, fall back to global
        p = await rps.GetReadingProfileDtoForSeries(user.Id, series3.LibraryId, series3.Id, deviceId);
        Assert.NotNull(p);
        Assert.Equal("Global", p.Name);
    }

    [Fact]
    public async Task GetCorrectProfile_ComplexHierarchy_NoDevice()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, lib, series) = await Setup(unitOfWork, context, mapper);

        const int deviceId = 1;

        var series2 = new SeriesBuilder("Rainbows After Storms").Build();
        lib.Series.Add(series2);

        var lib2 = new LibraryBuilder("Manga2").Build();
        var series3 = new SeriesBuilder("A Tropical Fish Yearns for Snow").Build();
        lib2.Series.Add(series3);
        user.Libraries.Add(lib2);

        // Mix of device and non-device profiles
        var profileSeries1NoDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Series 1 (No Device)")
            .Build();
        var profileSeries1Device = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(deviceId)
            .WithName("Series 1 (Device)")
            .Build();

        var profileLibNoDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithLibrary(lib)
            .WithName("Library (No Device)")
            .Build();
        var profileLibDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithLibrary(lib)
            .WithDeviceId(deviceId)
            .WithName("Library (Device)")
            .Build();

        context.AppUserReadingProfiles.Add(profileSeries1NoDevice);
        context.AppUserReadingProfiles.Add(profileSeries1Device);
        context.AppUserReadingProfiles.Add(profileLibNoDevice);
        context.AppUserReadingProfiles.Add(profileLibDevice);
        await unitOfWork.CommitAsync();

        // Without device specified, should always get no-device profiles
        var p = await rps.GetReadingProfileDtoForSeries(user.Id, series.LibraryId, series.Id, null);
        Assert.NotNull(p);
        Assert.Equal("Series 1 (No Device)", p.Name);

        p = await rps.GetReadingProfileDtoForSeries(user.Id, series2.LibraryId, series2.Id, null);
        Assert.NotNull(p);
        Assert.Equal("Library (No Device)", p.Name);

        p = await rps.GetReadingProfileDtoForSeries(user.Id, series3.LibraryId, series3.Id, null);
        Assert.NotNull(p);
        Assert.Equal("Global", p.Name);
    }

    [Fact]
    public async Task GetCorrectProfile_OnlyDeviceProfiles_NoMatch()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, lib, series) = await Setup(unitOfWork, context, mapper);

        const int deviceId = 1;
        const int otherDeviceId = 2;

        // Only profiles with specific devices exist
        var profileSeriesDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(otherDeviceId)
            .WithName("Series (Other Device)")
            .Build();
        var profileLibDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithLibrary(lib)
            .WithDeviceId(otherDeviceId)
            .WithName("Library (Other Device)")
            .Build();

        context.AppUserReadingProfiles.Add(profileSeriesDevice);
        context.AppUserReadingProfiles.Add(profileLibDevice);
        await unitOfWork.CommitAsync();

        // Should skip all device-specific profiles and fall back to global
        var p = await rps.GetReadingProfileDtoForSeries(user.Id, series.LibraryId, series.Id, deviceId);
        Assert.NotNull(p);
        Assert.Equal("Global", p.Name);
    }

    #endregion

    #region Tests with devices - Promote implicit profile

    [Fact]
    public async Task PromoteImplicitProfile_ConvertsImplicitToUser()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

        var implicitProfile = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Implicit Profile")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        context.AppUserReadingProfiles.Add(implicitProfile);
        await unitOfWork.CommitAsync();

        var result = await rps.PromoteImplicitProfile(user.Id, implicitProfile.Id, null);

        Assert.NotNull(result);
        Assert.Equal(ReadingProfileKind.User, result.Kind);

        // Verify in database
        var updatedProfile = await context.AppUserReadingProfiles.FindAsync(implicitProfile.Id);
        Assert.NotNull(updatedProfile);
        Assert.Equal(ReadingProfileKind.User, updatedProfile.Kind);
    }

    [Fact]
    public async Task PromoteImplicitProfile_WithDevice_OnlyRemovesFromMatchingDeviceProfiles()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

        const int deviceId = 1;
        const int otherDeviceId = 2;

        var implicitProfile = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(deviceId)
            .WithName("Implicit Profile (Device 1)")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        var profileSameDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(deviceId)
            .WithName("Profile (Device 1)")
            .Build();

        var profileOtherDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(otherDeviceId)
            .WithName("Profile (Device 2)")
            .Build();

        var profileNoDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Profile (No Device)")
            .Build();

        context.AppUserReadingProfiles.Add(implicitProfile);
        context.AppUserReadingProfiles.Add(profileSameDevice);
        context.AppUserReadingProfiles.Add(profileOtherDevice);
        context.AppUserReadingProfiles.Add(profileNoDevice);
        await unitOfWork.CommitAsync();

        await rps.PromoteImplicitProfile(user.Id, implicitProfile.Id, deviceId);

        // Same device profile should have series removed
        var updatedSameDevice = await context.AppUserReadingProfiles
            .FirstAsync(rp => rp.Id == profileSameDevice.Id);
        Assert.DoesNotContain(series.Id, updatedSameDevice.SeriesIds);

        // Other device profile should still have series
        var updatedOtherDevice = await context.AppUserReadingProfiles
            .FirstAsync(rp => rp.Id == profileOtherDevice.Id);
        Assert.Contains(series.Id, updatedOtherDevice.SeriesIds);

        // No device profile should still have series
        var updatedNoDevice = await context.AppUserReadingProfiles
            .FirstAsync(rp => rp.Id == profileNoDevice.Id);
        Assert.Contains(series.Id, updatedNoDevice.SeriesIds);
    }

    [Fact]
    public async Task PromoteImplicitProfile_DoesNotAffectOtherUsersProfiles()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, lib, series) = await Setup(unitOfWork, context, mapper);

        var otherUser = new AppUserBuilder("otheruser", "other@email.com").Build();
        context.AppUser.Add(otherUser);
        otherUser.Libraries.Add(lib);
        await unitOfWork.CommitAsync();

        var implicitProfile = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("User1 Implicit")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        var otherUserProfile = new AppUserReadingProfileBuilder(otherUser.Id)
            .WithSeries(series)
            .WithName("User2 Profile")
            .Build();

        context.AppUserReadingProfiles.Add(implicitProfile);
        context.AppUserReadingProfiles.Add(otherUserProfile);
        await unitOfWork.CommitAsync();

        await rps.PromoteImplicitProfile(user.Id, implicitProfile.Id, null);

        // Other user's profile should not be affected
        var updatedOtherUserProfile = await context.AppUserReadingProfiles
            .FirstAsync(rp => rp.Id == otherUserProfile.Id);
        Assert.Contains(series.Id, updatedOtherUserProfile.SeriesIds);
    }

    [Fact]
    public async Task PromoteImplicitProfile_DoesNotRemoveFromPromotedProfile()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

        var implicitProfile = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Implicit Profile")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        context.AppUserReadingProfiles.Add(implicitProfile);
        await unitOfWork.CommitAsync();

        await rps.PromoteImplicitProfile(user.Id, implicitProfile.Id, null);

        var updatedProfile = await context.AppUserReadingProfiles
            .FirstAsync(rp => rp.Id == implicitProfile.Id);
        Assert.Contains(series.Id, updatedProfile.SeriesIds);
    }

    [Fact]
    public async Task PromoteImplicitProfile_ThrowsIfProfileDoesNotBelongToUser()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

        var otherUser = new AppUserBuilder("otheruser", "other@email.com").Build();
        context.AppUser.Add(otherUser);
        await unitOfWork.CommitAsync();

        var implicitProfile = new AppUserReadingProfileBuilder(otherUser.Id)
            .WithSeries(series)
            .WithName("Other User's Implicit")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        context.AppUserReadingProfiles.Add(implicitProfile);
        await unitOfWork.CommitAsync();

        // Should throw when trying to promote another user's profile
        await Assert.ThrowsAsync<KavitaException>(async () =>
            await rps.PromoteImplicitProfile(user.Id, implicitProfile.Id, null));
    }

    [Fact]
    public async Task PromoteImplicitProfile_ThrowsIfProfileIsNotImplicit()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

        var userProfile = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("User Profile")
            .WithKind(ReadingProfileKind.User)
            .Build();

        context.AppUserReadingProfiles.Add(userProfile);
        await unitOfWork.CommitAsync();

        // Should throw when trying to promote a non-implicit profile
        await Assert.ThrowsAsync<KavitaException>(async () =>
            await rps.PromoteImplicitProfile(user.Id, userProfile.Id, null));
    }

    #endregion

    #region Tests with devices - Update parent profile

    [Fact]
    public async Task UpdateParent_WithDevice_OnlyRemovesMatchingImplicitProfiles()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, lib, series) = await Setup(unitOfWork, context, mapper);

        const int deviceId = 1;
        const int otherDeviceId = 2;

        var seriesProfile = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(deviceId)
            .WithName("Series Profile (Device 1)")
            .Build();

        var implicitSameDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(deviceId)
            .WithName("Implicit (Device 1)")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        var implicitOtherDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(otherDeviceId)
            .WithName("Implicit (Device 2)")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        var implicitNoDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Implicit (No Device)")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        context.AppUserReadingProfiles.Add(seriesProfile);
        context.AppUserReadingProfiles.Add(implicitSameDevice);
        context.AppUserReadingProfiles.Add(implicitOtherDevice);
        context.AppUserReadingProfiles.Add(implicitNoDevice);
        await unitOfWork.CommitAsync();

        var dto = new UserReadingProfileDto
        {
            Id = implicitSameDevice.Id,
            Name = "Updated Series Profile",
        };

        await rps.UpdateParent(user.Id, lib.Id, series.Id, dto, deviceId);

        // Implicit with same device should be removed
        var implicitSameExists = await context.AppUserReadingProfiles
            .AnyAsync(rp => rp.Id == implicitSameDevice.Id);
        Assert.False(implicitSameExists);

        // Implicit with other device should still exist
        var implicitOtherExists = await context.AppUserReadingProfiles
            .AnyAsync(rp => rp.Id == implicitOtherDevice.Id);
        Assert.True(implicitOtherExists);

        // Implicit with no device should still exist (it's a fallback for other devices)
        var implicitNoDeviceExists = await context.AppUserReadingProfiles
            .AnyAsync(rp => rp.Id == implicitNoDevice.Id);
        Assert.True(implicitNoDeviceExists);
    }

    [Fact]
    public async Task UpdateParent_NoDevice_OnlyRemovesNoDeviceImplicitProfiles()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, lib, series) = await Setup(unitOfWork, context, mapper);

        const int deviceId = 1;

        var seriesProfile = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Series Profile (No Device)")
            .Build();

        var implicitWithDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(deviceId)
            .WithName("Implicit (Device)")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        var implicitNoDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Implicit (No Device)")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        context.AppUserReadingProfiles.Add(seriesProfile);
        context.AppUserReadingProfiles.Add(implicitWithDevice);
        context.AppUserReadingProfiles.Add(implicitNoDevice);
        await unitOfWork.CommitAsync();

        var dto = new UserReadingProfileDto
        {
            Id = implicitNoDevice.Id,
            Name = "Updated Series Profile",
        };

        await rps.UpdateParent(user.Id, lib.Id, series.Id, dto, null);

        // Implicit with no device should be removed
        var implicitNoDeviceExists = await context.AppUserReadingProfiles
            .AnyAsync(rp => rp.Id == implicitNoDevice.Id);
        Assert.False(implicitNoDeviceExists);

        // Implicit with device should still exist (other devices may use it)
        var implicitWithDeviceExists = await context.AppUserReadingProfiles
            .AnyAsync(rp => rp.Id == implicitWithDevice.Id);
        Assert.True(implicitWithDeviceExists);
    }

    [Fact]
    public async Task UpdateParent_WithDevice_AllowsMultipleDeviceSpecificImplicits()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, lib, series) = await Setup(unitOfWork, context, mapper);

        const int device1 = 1;
        const int device2 = 2;
        const int device3 = 3;

        var seriesProfile = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(device1)
            .WithName("Series Profile (Device 1)")
            .Build();

        var implicitDevice1 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(device1)
            .WithName("Implicit (Device 1)")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        var implicitDevice2 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(device2)
            .WithName("Implicit (Device 2)")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        var implicitDevice3 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(device3)
            .WithName("Implicit (Device 3)")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        var implicitNoDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Implicit (No Device - Fallback)")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        context.AppUserReadingProfiles.Add(seriesProfile);
        context.AppUserReadingProfiles.Add(implicitDevice1);
        context.AppUserReadingProfiles.Add(implicitDevice2);
        context.AppUserReadingProfiles.Add(implicitDevice3);
        context.AppUserReadingProfiles.Add(implicitNoDevice);
        await unitOfWork.CommitAsync();

        var dto = new UserReadingProfileDto
        {
            Id = implicitDevice1.Id,
            Name = "Updated Series Profile",
        };

        await rps.UpdateParent(user.Id, lib.Id, series.Id, dto, device1);

        // Only implicit for device 1 should be removed
        var implicit1Exists = await context.AppUserReadingProfiles
            .AnyAsync(rp => rp.Id == implicitDevice1.Id);
        Assert.False(implicit1Exists);

        // Other device-specific implicits should remain
        var implicit2Exists = await context.AppUserReadingProfiles
            .AnyAsync(rp => rp.Id == implicitDevice2.Id);
        Assert.True(implicit2Exists);

        var implicit3Exists = await context.AppUserReadingProfiles
            .AnyAsync(rp => rp.Id == implicitDevice3.Id);
        Assert.True(implicit3Exists);

        // Fallback (no device) should remain
        var implicitNoDeviceExists = await context.AppUserReadingProfiles
            .AnyAsync(rp => rp.Id == implicitNoDevice.Id);
        Assert.True(implicitNoDeviceExists);
    }

    #endregion

    #region Tests with devices - AddToProfile

    [Fact]
    public async Task AddProfileToSeries_WithDevice_OnlyRemovesImplicitWithSameDevice()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

        const int deviceId = 1;
        const int otherDeviceId = 2;

        var profile = new AppUserReadingProfileBuilder(user.Id)
            .WithDeviceId(deviceId)
            .WithName("Profile (Device 1)")
            .Build();

        var implicitSameDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(deviceId)
            .WithName("Implicit (Device 1)")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        var implicitOtherDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(otherDeviceId)
            .WithName("Implicit (Device 2)")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        var implicitNoDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Implicit (No Device)")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        context.AppUserReadingProfiles.Add(profile);
        context.AppUserReadingProfiles.Add(implicitSameDevice);
        context.AppUserReadingProfiles.Add(implicitOtherDevice);
        context.AppUserReadingProfiles.Add(implicitNoDevice);
        await unitOfWork.CommitAsync();

        await rps.SetSeriesProfiles(user.Id, [profile.Id], series.Id);

        // Implicit with same device should be removed
        var implicitSameExists = await context.AppUserReadingProfiles
            .AnyAsync(rp => rp.Id == implicitSameDevice.Id);
        Assert.False(implicitSameExists);

        // Implicit with other device should still exist
        var implicitOtherExists = await context.AppUserReadingProfiles
            .AnyAsync(rp => rp.Id == implicitOtherDevice.Id);
        Assert.True(implicitOtherExists);

        // Implicit with no device should still exist (fallback for other devices)
        var implicitNoDeviceExists = await context.AppUserReadingProfiles
            .AnyAsync(rp => rp.Id == implicitNoDevice.Id);
        Assert.True(implicitNoDeviceExists);
    }

    [Fact]
    public async Task AddProfileToSeries_NoDevice_OnlyRemovesImplicitWithNoDevice()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

        const int deviceId = 1;

        var profile = new AppUserReadingProfileBuilder(user.Id)
            .WithName("Profile (No Device)")
            .Build();

        var implicitWithDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(deviceId)
            .WithName("Implicit (Device)")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        var implicitNoDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Implicit (No Device)")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        context.AppUserReadingProfiles.Add(profile);
        context.AppUserReadingProfiles.Add(implicitWithDevice);
        context.AppUserReadingProfiles.Add(implicitNoDevice);
        await unitOfWork.CommitAsync();

        await rps.SetSeriesProfiles(user.Id, [profile.Id], series.Id);

        // Implicit with no device should be removed
        var implicitNoDeviceExists = await context.AppUserReadingProfiles
            .AnyAsync(rp => rp.Id == implicitNoDevice.Id);
        Assert.False(implicitNoDeviceExists);

        // Implicit with device should still exist (for that specific device)
        var implicitWithDeviceExists = await context.AppUserReadingProfiles
            .AnyAsync(rp => rp.Id == implicitWithDevice.Id);
        Assert.True(implicitWithDeviceExists);
    }

    [Fact]
    public async Task AddProfileToSeries_WithMultipleDevices_RemovesImplicitsForAllDevices()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

        const int device1 = 1;
        const int device2 = 2;
        const int device3 = 3;

        var profile = new AppUserReadingProfileBuilder(user.Id)
            .WithDeviceId(device1)
            .WithDeviceId(device2)
            .WithName("Profile (Device 1 & 2)")
            .Build();

        var implicitDevice1 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(device1)
            .WithName("Implicit (Device 1)")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        var implicitDevice2 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(device2)
            .WithName("Implicit (Device 2)")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        var implicitDevice3 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(device3)
            .WithName("Implicit (Device 3)")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        var implicitNoDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Implicit (No Device)")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        context.AppUserReadingProfiles.Add(profile);
        context.AppUserReadingProfiles.Add(implicitDevice1);
        context.AppUserReadingProfiles.Add(implicitDevice2);
        context.AppUserReadingProfiles.Add(implicitDevice3);
        context.AppUserReadingProfiles.Add(implicitNoDevice);
        await unitOfWork.CommitAsync();

        await rps.SetSeriesProfiles(user.Id, [profile.Id], series.Id);

        // Implicits for device 1 and 2 should be removed
        var implicit1Exists = await context.AppUserReadingProfiles
            .AnyAsync(rp => rp.Id == implicitDevice1.Id);
        Assert.False(implicit1Exists);

        var implicit2Exists = await context.AppUserReadingProfiles
            .AnyAsync(rp => rp.Id == implicitDevice2.Id);
        Assert.False(implicit2Exists);

        // Implicit for device 3 should still exist
        var implicit3Exists = await context.AppUserReadingProfiles
            .AnyAsync(rp => rp.Id == implicitDevice3.Id);
        Assert.True(implicit3Exists);

        // Implicit with no device should still exist (fallback)
        var implicitNoDeviceExists = await context.AppUserReadingProfiles
            .AnyAsync(rp => rp.Id == implicitNoDevice.Id);
        Assert.True(implicitNoDeviceExists);
    }

    [Fact]
    public async Task AddProfileToSeries_ImplicitWithMultipleDevices_RemovedIfAnyDeviceMatches()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

        const int device1 = 1;
        const int device2 = 2;
        const int device3 = 3;

        var profile = new AppUserReadingProfileBuilder(user.Id)
            .WithDeviceId(device1)
            .WithName("Profile (Device 1)")
            .Build();

        // Implicit with multiple devices including device1
        var implicitMultiDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(device1)
            .WithDeviceId(device2)
            .WithName("Implicit (Device 1 & 2)")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        // Implicit with multiple devices NOT including device1
        var implicitOtherDevices = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(device2)
            .WithDeviceId(device3)
            .WithName("Implicit (Device 2 & 3)")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        context.AppUserReadingProfiles.Add(profile);
        context.AppUserReadingProfiles.Add(implicitMultiDevice);
        context.AppUserReadingProfiles.Add(implicitOtherDevices);
        await unitOfWork.CommitAsync();

        await rps.SetSeriesProfiles(user.Id, [profile.Id], series.Id);

        // Implicit with device1 (among others) should be removed
        var implicitMultiExists = await context.AppUserReadingProfiles
            .AnyAsync(rp => rp.Id == implicitMultiDevice.Id);
        Assert.False(implicitMultiExists);

        // Implicit without device1 should still exist
        var implicitOtherExists = await context.AppUserReadingProfiles
            .AnyAsync(rp => rp.Id == implicitOtherDevices.Id);
        Assert.True(implicitOtherExists);
    }

    [Fact]
    public async Task AddProfileToSeries_ThrowsIfProfileDoesNotBelongToUser()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

        var otherUser = new AppUserBuilder("otheruser", "other@email.com").Build();
        context.AppUser.Add(otherUser);
        await unitOfWork.CommitAsync();

        var otherUserProfile = new AppUserReadingProfileBuilder(otherUser.Id)
            .WithName("Other User's Profile")
            .Build();

        context.AppUserReadingProfiles.Add(otherUserProfile);
        await unitOfWork.CommitAsync();

        // Should throw when trying to add series to another user's profile
        await Assert.ThrowsAsync<KavitaException>(async () =>
            await rps.SetSeriesProfiles(user.Id, [otherUserProfile.Id], series.Id));
    }

    #endregion

    #region Tests with devices - ClearSeriesProfile

    [Fact]
    public async Task ClearSeriesProfile_RemovesAllProfilesForSeriesRegardlessOfKindOrDevice()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

        var userProfileDevice1 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(1)
            .WithName("User (Device 1)")
            .Build();

        var userProfileDevice2 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(2)
            .WithName("User (Device 2)")
            .Build();

        var implicitProfileDevice1 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(1)
            .WithName("Implicit (Device 1)")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        var implicitProfileNoDevice = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Implicit (No Device)")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        context.AppUserReadingProfiles.AddRange(
            userProfileDevice1,
            userProfileDevice2,
            implicitProfileDevice1,
            implicitProfileNoDevice
        );
        await unitOfWork.CommitAsync();

        await rps.ClearSeriesProfile(user.Id, series.Id);

        var remainingProfiles = await context.AppUserReadingProfiles
            .Where(rp => rp.SeriesIds.Contains(series.Id))
            .ToListAsync();

        Assert.Empty(remainingProfiles);
    }


    #endregion

    #region Tests with devices - SetProfileDevices

    [Fact]
    public async Task SetProfileDevices_Basic()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

        const int device1 = 1;
        const int device2 = 2;
        const int device3 = 3;

        var profile = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(device1)
            .WithDeviceId(device2)
            .WithName("Profile")
            .Build();

        context.AppUserReadingProfiles.Add(profile);
        await unitOfWork.CommitAsync();

        // Sets both
        await rps.SetProfileDevices(user.Id, profile.Id, [device1, device2]);

        var updated = await context.AppUserReadingProfiles
            .FirstAsync(rp => rp.Id == profile.Id);

        Assert.Equal(2, updated.DeviceIds.Count);
        Assert.Contains(device1, updated.DeviceIds);
        Assert.Contains(device2, updated.DeviceIds);

        // Full replace
        await rps.SetProfileDevices(user.Id, profile.Id, [device3]);

        updated = await context.AppUserReadingProfiles

            .FirstAsync(rp => rp.Id == profile.Id);

        Assert.Single(updated.DeviceIds);
        Assert.Contains(device3, updated.DeviceIds);
        Assert.DoesNotContain(device1, updated.DeviceIds);
        Assert.DoesNotContain(device2, updated.DeviceIds);

        // Allow empty
        await rps.SetProfileDevices(user.Id, profile.Id, []);

        updated = await context.AppUserReadingProfiles

            .FirstAsync(rp => rp.Id == profile.Id);

        Assert.Empty(updated.DeviceIds);
    }

    [Fact]
    public async Task SetProfileDevices_RemovesDuplicateSeriesLinks_SameDevice()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

        const int device1 = 1;

        var profile1 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(device1)
            .WithName("Profile 1")
            .Build();

        var profile2 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Profile 2 (will get device 1)")
            .Build();

        context.AppUserReadingProfiles.Add(profile1);
        context.AppUserReadingProfiles.Add(profile2);
        await unitOfWork.CommitAsync();

        // Add device1 to profile2, which should remove series from profile1
        await rps.SetProfileDevices(user.Id, profile2.Id, [device1]);

        var updatedProfile1 = await context.AppUserReadingProfiles

            .FirstAsync(rp => rp.Id == profile1.Id);

        Assert.DoesNotContain(series.Id, updatedProfile1.SeriesIds);

        var updatedProfile2 = await context.AppUserReadingProfiles

            .FirstAsync(rp => rp.Id == profile2.Id);

        Assert.Contains(series.Id, updatedProfile2.SeriesIds);
    }

    [Fact]
    public async Task SetProfileDevices_RemovesDuplicateSeriesLinks_MultipleDevices()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

        const int device1 = 1;
        const int device2 = 2;
        const int device3 = 3;

        var profile1 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(device1)
            .WithDeviceId(device2)
            .WithName("Profile 1 (Device 1 & 2)")
            .Build();

        var profile2 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(device3)
            .WithName("Profile 2 (will get Device 2)")
            .Build();

        context.AppUserReadingProfiles.Add(profile1);
        context.AppUserReadingProfiles.Add(profile2);
        await unitOfWork.CommitAsync();

        // Add device2 to profile2, which should remove series from profile1 only for device2
        await rps.SetProfileDevices(user.Id, profile2.Id, [device2]);

        var updatedProfile1 = await context.AppUserReadingProfiles


            .FirstAsync(rp => rp.Id == profile1.Id);

        // Profile1 should no longer have the series since device2 now conflicts
        Assert.DoesNotContain(series.Id, updatedProfile1.SeriesIds);
    }

    [Fact]
    public async Task SetProfileDevices_RemovesDuplicateLibraryLinks()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, lib, _) = await Setup(unitOfWork, context, mapper);

        const int device1 = 1;

        var profile1 = new AppUserReadingProfileBuilder(user.Id)
            .WithLibrary(lib)
            .WithDeviceId(device1)
            .WithName("Profile 1")
            .Build();

        var profile2 = new AppUserReadingProfileBuilder(user.Id)
            .WithLibrary(lib)
            .WithName("Profile 2 (will get device 1)")
            .Build();

        context.AppUserReadingProfiles.Add(profile1);
        context.AppUserReadingProfiles.Add(profile2);
        await unitOfWork.CommitAsync();

        await rps.SetProfileDevices(user.Id, profile2.Id, [device1]);

        var updatedProfile1 = await context.AppUserReadingProfiles

            .FirstAsync(rp => rp.Id == profile1.Id);

        Assert.DoesNotContain(lib.Id, updatedProfile1.LibraryIds);

        var updatedProfile2 = await context.AppUserReadingProfiles

            .FirstAsync(rp => rp.Id == profile2.Id);

        Assert.Contains(lib.Id, updatedProfile2.LibraryIds);
    }

    [Fact]
    public async Task SetProfileDevices_RemovesDuplicates_BothSeriesAndLibrary()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

        var lib2 = new LibraryBuilder("Library 2").Build();
        user.Libraries.Add(lib2);

        const int device1 = 1;

        var profile1 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithLibrary(lib2)
            .WithDeviceId(device1)
            .WithName("Profile 1")
            .Build();

        var profile2 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithLibrary(lib2)
            .WithName("Profile 2 (will get device 1)")
            .Build();

        context.AppUserReadingProfiles.Add(profile1);
        context.AppUserReadingProfiles.Add(profile2);
        await unitOfWork.CommitAsync();

        await rps.SetProfileDevices(user.Id, profile2.Id, [device1]);

        var updatedProfile1 = await context.AppUserReadingProfiles


            .FirstAsync(rp => rp.Id == profile1.Id);

        Assert.DoesNotContain(series.Id, updatedProfile1.SeriesIds);
        Assert.DoesNotContain(lib2.Id, updatedProfile1.LibraryIds);
    }

    [Fact]
    public async Task SetProfileDevices_OnlyRemovesConflictingDeviceLinks()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, lib, series) = await Setup(unitOfWork, context, mapper);

        var series2 = new SeriesBuilder("Series 2").Build();
        lib.Series.Add(series2);
        await unitOfWork.CommitAsync();

        const int device1 = 1;

        var profile1 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithSeries(series2)
            .WithDeviceId(device1)
            .WithName("Profile 1 (Device 1)")
            .Build();

        var profile2 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Profile 2 (will get device 1)")
            .Build();

        context.AppUserReadingProfiles.Add(profile1);
        context.AppUserReadingProfiles.Add(profile2);
        await unitOfWork.CommitAsync();

        await rps.SetProfileDevices(user.Id, profile2.Id, [device1]);

        var updatedProfile1 = await context.AppUserReadingProfiles
            .FirstAsync(rp => rp.Id == profile1.Id);

        // Only series1 should be removed, series2 should remain
        Assert.DoesNotContain(series.Id, updatedProfile1.SeriesIds);
        Assert.Contains(series2.Id, updatedProfile1.SeriesIds);
    }

    [Fact]
    public async Task SetProfileDevices_DoesNotAffectNoDeviceProfiles()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

        const int device1 = 1;

        var profile1 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Profile 1 (No Device - Fallback)")
            .Build();

        var profile2 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Profile 2 (will get device 1)")
            .Build();

        context.AppUserReadingProfiles.Add(profile1);
        context.AppUserReadingProfiles.Add(profile2);
        await unitOfWork.CommitAsync();

        await rps.SetProfileDevices(user.Id, profile2.Id, [device1]);

        var updatedProfile1 = await context.AppUserReadingProfiles
            .FirstAsync(rp => rp.Id == profile1.Id);

        // Profile1 has no devices, so it's a fallback and should not be affected
        Assert.Contains(series.Id, updatedProfile1.SeriesIds);
    }

    [Fact]
    public async Task SetProfileDevices_SettingToNoDevice_RemovesDuplicatesWithNoDevice()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

        const int device1 = 1;

        var profile1 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Profile 1 (No Device)")
            .Build();

        var profile2 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(device1)
            .WithName("Profile 2 (Device 1, will remove device)")
            .Build();

        context.AppUserReadingProfiles.Add(profile1);
        context.AppUserReadingProfiles.Add(profile2);
        await unitOfWork.CommitAsync();

        // Set profile2 to no devices, which conflicts with profile1's no-device fallback
        await rps.SetProfileDevices(user.Id, profile2.Id, []);

        var updatedProfile1 = await context.AppUserReadingProfiles

            .FirstAsync(rp => rp.Id == profile1.Id);

        Assert.DoesNotContain(series.Id, updatedProfile1.SeriesIds);
    }

    [Fact]
    public async Task SetProfileDevices_RemovesMultipleConflicts()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

        const int device1 = 1;

        var profile1 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(device1)
            .WithName("Profile 1")
            .Build();

        var profile2 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(device1)
            .WithName("Profile 2")
            .Build();

        var profile3 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("Profile 3 (will get device 1)")
            .Build();

        context.AppUserReadingProfiles.Add(profile1);
        context.AppUserReadingProfiles.Add(profile2);
        context.AppUserReadingProfiles.Add(profile3);
        await unitOfWork.CommitAsync();

        await rps.SetProfileDevices(user.Id, profile3.Id, [device1]);

        var updatedProfile1 = await context.AppUserReadingProfiles

            .FirstAsync(rp => rp.Id == profile1.Id);

        var updatedProfile2 = await context.AppUserReadingProfiles

            .FirstAsync(rp => rp.Id == profile2.Id);

        // Both profile1 and profile2 should have series removed
        Assert.DoesNotContain(series.Id, updatedProfile1.SeriesIds);
        Assert.DoesNotContain(series.Id, updatedProfile2.SeriesIds);
    }

    [Fact]
    public async Task SetProfileDevices_DoesNotRemoveImplicitProfiles()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, series) = await Setup(unitOfWork, context, mapper);

        const int device1 = 1;

        var implicitProfile = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithDeviceId(device1)
            .WithName("Implicit Profile")
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        var userProfile = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithName("User Profile (will get device 1)")
            .Build();

        context.AppUserReadingProfiles.Add(implicitProfile);
        context.AppUserReadingProfiles.Add(userProfile);
        await unitOfWork.CommitAsync();

        await rps.SetProfileDevices(user.Id, userProfile.Id, [device1]);

        var updatedImplicit = await context.AppUserReadingProfiles
            .FirstAsync(rp => rp.Id == implicitProfile.Id);

        // Implicit profiles should not be affected by duplicate removal
        Assert.Contains(series.Id, updatedImplicit.SeriesIds);
    }

    [Fact]
    public async Task SetProfileDevices_WorksWithComplexScenario()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, lib, series) = await Setup(unitOfWork, context, mapper);

        var series2 = new SeriesBuilder("Series 2").Build();
        var series3 = new SeriesBuilder("Series 3").Build();
        lib.Series.Add(series2);
        lib.Series.Add(series3);
        await unitOfWork.CommitAsync();

        const int device1 = 1;
        const int device2 = 2;
        const int device3 = 3;

        var profile1 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithSeries(series2)
            .WithDeviceId(device1)
            .WithDeviceId(device2)
            .WithName("Profile 1 (Device 1 & 2)")
            .Build();

        var profile2 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series)
            .WithSeries(series3)
            .WithDeviceId(device3)
            .WithName("Profile 2 (will get Device 2)")
            .Build();

        context.AppUserReadingProfiles.Add(profile1);
        context.AppUserReadingProfiles.Add(profile2);
        await unitOfWork.CommitAsync();

        // Set profile2 to device2, which conflicts with profile1
        await rps.SetProfileDevices(user.Id, profile2.Id, [device2]);

        var updatedProfile1 = await context.AppUserReadingProfiles

            .FirstAsync(rp => rp.Id == profile1.Id);

        // Profile1 should lose series1 (conflicts), but keep series2 (no conflict)
        Assert.DoesNotContain(series.Id, updatedProfile1.SeriesIds);
        Assert.Contains(series2.Id, updatedProfile1.SeriesIds);

        var updatedProfile2 = await context.AppUserReadingProfiles


            .FirstAsync(rp => rp.Id == profile2.Id);

        // Profile2 should have device2 and keep both series
        Assert.Single(updatedProfile2.DeviceIds);
        Assert.Contains(device2, updatedProfile2.DeviceIds);
        Assert.Contains(series.Id, updatedProfile2.SeriesIds);
        Assert.Contains(series3.Id, updatedProfile2.SeriesIds);
    }

    [Fact]
    public async Task SetProfileDevices_ThrowsIfProfileDoesNotBelongToUser()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (rps, user, _, _) = await Setup(unitOfWork, context, mapper);

        var otherUser = new AppUserBuilder("otheruser", "other@email.com").Build();
        context.AppUser.Add(otherUser);
        await unitOfWork.CommitAsync();

        const int device1 = 1;

        var otherUserProfile = new AppUserReadingProfileBuilder(otherUser.Id)
            .WithName("Other User's Profile")
            .Build();

        context.AppUserReadingProfiles.Add(otherUserProfile);
        await unitOfWork.CommitAsync();

        // Should throw when trying to set devices on another user's profile
        await Assert.ThrowsAsync<KavitaException>(async () =>
            await rps.SetProfileDevices(user.Id, otherUserProfile.Id, [device1]));
    }

    #endregion

    /// <summary>
    /// As response to #3793, I'm not sure if we want to keep this. It's not the most nice. But I think the idea of this test
    /// is worth having.
    /// </summary>
    [Fact]
    public async Task UpdateFields_UpdatesAll()
    {
        var (_, _, mapper) = await CreateDatabase();

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
