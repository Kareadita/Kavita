using System.Threading.Tasks;
using API.Data.Repositories;
using API.DTOs;
using API.Entities;
using API.Entities.Enums;
using API.Helpers.Builders;
using API.Services;
using Kavita.Common;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace API.Tests.Services;

public class ReadingProfileServiceTest: AbstractDbTest
{

    public async Task<(IReadingProfileService, AppUser, Library, Series)> Setup()
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

        var rps = new ReadingProfileService(UnitOfWork);
        user = await UnitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.UserPreferences);

        return (rps, user, library, series);
    }

    [Fact]
    public async Task DeleteImplicitSeriesReadingProfile()
    {
        await ResetDb();
        var (rps, user, library, series) = await Setup();

        var series2 = new SeriesBuilder("Rainbows After Storms").Build();
        library.Series.Add(series2);

        var profile = new AppUserReadingProfileBuilder(user.Id)
            .WithImplicit(true)
            .WithSeries(series)
            .Build();

        var profile2 = new AppUserReadingProfileBuilder(user.Id)
            .WithSeries(series2)
            .Build();

        UnitOfWork.AppUserReadingProfileRepository.Add(profile);
        UnitOfWork.AppUserReadingProfileRepository.Add(profile2);

        await UnitOfWork.CommitAsync();

        await rps.DeleteImplicitForSeries(user.Id, series.Id);
        await rps.DeleteImplicitForSeries(user.Id, series2.Id);

        profile = await UnitOfWork.AppUserReadingProfileRepository.GetProfile(profile.Id);
        Assert.NotNull(profile);
        Assert.Empty(profile.Series);

        profile2 = await UnitOfWork.AppUserReadingProfileRepository.GetProfile(profile2.Id);
        Assert.NotNull(profile2);
        Assert.NotEmpty(profile2.Series);
    }

    [Fact]
    public async Task CantDeleteDefaultReadingProfile()
    {
        await ResetDb();
        var (rps, user, _, _) = await Setup();

        var profile = new AppUserReadingProfileBuilder(user.Id).Build();
        Context.AppUserReadingProfile.Add(profile);
        await UnitOfWork.CommitAsync();

        user.UserPreferences.DefaultReadingProfileId = profile.Id;
        await UnitOfWork.CommitAsync();

        await Assert.ThrowsAsync<KavitaException>(async () =>
        {
            await rps.DeleteReadingProfile(user.Id, profile.Id);
        });

        var profile2 = new AppUserReadingProfileBuilder(user.Id).Build();
        Context.AppUserReadingProfile.Add(profile2);
        await UnitOfWork.CommitAsync();

        await rps.DeleteReadingProfile(user.Id, profile2.Id);
        await UnitOfWork.CommitAsync();

        var allProfiles = await Context.AppUserReadingProfile.ToListAsync();
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

        await rps.UpdateReadingProfileForSeries(user.Id, series.Id, dto);

        var profile = await UnitOfWork.AppUserReadingProfileRepository.GetProfileForSeries(user.Id, series.Id);
        Assert.NotNull(profile);
        Assert.Contains(profile.Series, s => s.Id == series.Id);
        Assert.True(profile.Implicit);
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
            .WithName("Global")
            .Build();
        Context.AppUserReadingProfile.Add(profile);
        Context.AppUserReadingProfile.Add(profile2);
        Context.AppUserReadingProfile.Add(profile3);

        var series2 = new SeriesBuilder("Rainbows After Storms").Build();
        lib.Series.Add(series2);

        var lib2 = new LibraryBuilder("Manga2").Build();
        var series3 = new SeriesBuilder("A Tropical Fish Yearns for Snow").WithLibraryId(lib2.Id).Build();

        user.Libraries.Add(lib2);
        await UnitOfWork.CommitAsync();

        user.UserPreferences.DefaultReadingProfileId = profile3.Id;
        await UnitOfWork.CommitAsync();

        var p = await rps.GetReadingProfileForSeries(user.Id, series);
        Assert.NotNull(p);
        Assert.Equal("Series Specific", p.Name);

        p = await rps.GetReadingProfileForSeries(user.Id, series2);
        Assert.NotNull(p);
        Assert.Equal("Library Specific", p.Name);

        p = await rps.GetReadingProfileForSeries(user.Id, series3);
        Assert.NotNull(p);
        Assert.Equal("Global", p.Name);
    }

    protected override async Task ResetDb()
    {
        Context.AppUserReadingProfile.RemoveRange(Context.AppUserReadingProfile);
        await UnitOfWork.CommitAsync();
    }
}
