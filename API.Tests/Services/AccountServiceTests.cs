using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers.Builders;
using API.Services;
using API.Services.Tasks.Scanner;
using Kavita.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;

public class AccountServiceTests: AbstractDbTest
{

    [Theory]
    [InlineData("admin", true)]
    [InlineData("^^$SomeBadChars", false)]
    [InlineData("Lisa2003", true)]
    [InlineData("Kraft Lawrance", false)]
    public async Task ValidateUsername_Regex(string username, bool valid)
    {
        await ResetDb();
        var (_, accountService, _, _) = await Setup();

        Assert.Equal(valid, !(await accountService.ValidateUsername(username)).Any());
    }

    [Fact]
    public async Task ChangeIdentityProvider_Throws_WhenDefaultAdminUser()
    {
        await ResetDb();
        var (_, accountService, _, _) = await Setup();

        var defaultAdmin = await UnitOfWork.UserRepository.GetDefaultAdminUser();

        await Assert.ThrowsAsync<KavitaException>(() =>
            accountService.ChangeIdentityProvider(defaultAdmin.Id, defaultAdmin, IdentityProvider.Kavita));
    }

    [Fact]
    public async Task ChangeIdentityProvider_Succeeds_WhenSyncUserSettingsIsFalse()
    {
        await ResetDb();
        var (user, accountService, _, _) = await Setup();

        var result = await accountService.ChangeIdentityProvider(user.Id, user, IdentityProvider.Kavita);

        Assert.False(result);

        var updated = await UnitOfWork.UserRepository.GetUserByIdAsync(user.Id);
        Assert.NotNull(updated);
        Assert.Equal(IdentityProvider.Kavita, updated.IdentityProvider);
    }

    [Fact]
    public async Task ChangeIdentityProvider_Throws_WhenUserIsOidcManaged_AndNoChange()
    {
        await ResetDb();
        var (user, accountService, _, settingsService) = await Setup();

        user.IdentityProvider = IdentityProvider.OpenIdConnect;
        await UnitOfWork.CommitAsync();

        var settings = await UnitOfWork.SettingsRepository.GetSettingsDtoAsync();
        settings.OidcConfig.SyncUserSettings = true;
        await settingsService.UpdateSettings(settings);

        await Assert.ThrowsAsync<KavitaException>(() =>
            accountService.ChangeIdentityProvider(user.Id, user, IdentityProvider.OpenIdConnect));
    }

    [Fact]
    public async Task ChangeIdentityProvider_Succeeds_WhenSyncUserSettingsTrue_AndChangeIsAllowed()
    {
        await ResetDb();
        var (user, accountService, _, settingsService) = await Setup();

        user.IdentityProvider = IdentityProvider.OpenIdConnect;
        await UnitOfWork.CommitAsync();

        var settings = await UnitOfWork.SettingsRepository.GetSettingsDtoAsync();
        settings.OidcConfig.SyncUserSettings = true;
        await settingsService.UpdateSettings(settings);

        var result = await accountService.ChangeIdentityProvider(user.Id, user, IdentityProvider.Kavita);

        Assert.False(result);

        var updated = await UnitOfWork.UserRepository.GetUserByIdAsync(user.Id);
        Assert.NotNull(updated);
        Assert.Equal(IdentityProvider.Kavita, updated.IdentityProvider);
    }

    [Fact]
    public async Task ChangeIdentityProvider_ReturnsTrue_WhenChangedToOidc()
    {
        await ResetDb();
        var (user, accountService, _, settingsService) = await Setup();

        user.IdentityProvider = IdentityProvider.Kavita;
        await UnitOfWork.CommitAsync();

        var settings = await UnitOfWork.SettingsRepository.GetSettingsDtoAsync();
        settings.OidcConfig.SyncUserSettings = true;
        await settingsService.UpdateSettings(settings);

        var result = await accountService.ChangeIdentityProvider(user.Id, user, IdentityProvider.OpenIdConnect);

        Assert.True(result);

        var updated = await UnitOfWork.UserRepository.GetUserByIdAsync(user.Id);
        Assert.NotNull(updated);
        Assert.Equal(IdentityProvider.OpenIdConnect, updated.IdentityProvider);
    }

    [Fact]
    public async Task UpdateLibrariesForUser_GrantsAccessToAllLibraries_WhenAdmin()
    {
        await ResetDb();
        var (user, accountService, _, _) = await Setup();

        var mangaLib = new LibraryBuilder("Manga", LibraryType.Manga).Build();
        var lightNovelsLib = new LibraryBuilder("Light Novels", LibraryType.LightNovel).Build();

        UnitOfWork.LibraryRepository.Add(mangaLib);
        UnitOfWork.LibraryRepository.Add(lightNovelsLib);
        await UnitOfWork.CommitAsync();

        await accountService.UpdateLibrariesForUser(user, new List<int>(), hasAdminRole: true);
        await UnitOfWork.CommitAsync();

        var userLibs = await UnitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(user.Id);
        Assert.Equal(2, userLibs.Count());
    }

    [Fact]
    public async Task UpdateLibrariesForUser_GrantsAccessToSelectedLibraries_WhenNotAdmin()
    {
        await ResetDb();
        var (user, accountService, _, _) = await Setup();

        var mangaLib = new LibraryBuilder("Manga", LibraryType.Manga).Build();
        var lightNovelsLib = new LibraryBuilder("Light Novels", LibraryType.LightNovel).Build();

        UnitOfWork.LibraryRepository.Add(mangaLib);
        UnitOfWork.LibraryRepository.Add(lightNovelsLib);
        await UnitOfWork.CommitAsync();

        await accountService.UpdateLibrariesForUser(user, new List<int> { mangaLib.Id }, hasAdminRole: false);
        await UnitOfWork.CommitAsync();

        var userLibs = (await UnitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(user.Id)).ToList();
        Assert.Single(userLibs);
        Assert.Equal(mangaLib.Id, userLibs.First().Id);
    }

    [Fact]
    public async Task UpdateLibrariesForUser_RemovesAccessFromUnselectedLibraries_WhenNotAdmin()
    {
        await ResetDb();
        var (user, accountService, _, _) = await Setup();

        var mangaLib = new LibraryBuilder("Manga", LibraryType.Manga).Build();
        var lightNovelsLib = new LibraryBuilder("Light Novels", LibraryType.LightNovel).Build();

        UnitOfWork.LibraryRepository.Add(mangaLib);
        UnitOfWork.LibraryRepository.Add(lightNovelsLib);
        await UnitOfWork.CommitAsync();

        // Grant access to both libraries
        await accountService.UpdateLibrariesForUser(user, new List<int> { mangaLib.Id, lightNovelsLib.Id }, hasAdminRole: false);
        await UnitOfWork.CommitAsync();

        var userLibs = (await UnitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(user.Id)).ToList();
        Assert.Equal(2, userLibs.Count);

        // Now restrict access to only light novels
        await accountService.UpdateLibrariesForUser(user, new List<int> { lightNovelsLib.Id }, hasAdminRole: false);
        await UnitOfWork.CommitAsync();

        userLibs = (await UnitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(user.Id)).ToList();
        Assert.Single(userLibs);
        Assert.Equal(lightNovelsLib.Id, userLibs.First().Id);
    }

    [Fact]
    public async Task UpdateLibrariesForUser_GrantsNoLibraries_WhenNoneSelected_AndNotAdmin()
    {
        await ResetDb();
        var (user, accountService, _, _) = await Setup();

        var mangaLib = new LibraryBuilder("Manga", LibraryType.Manga).Build();
        var lightNovelsLib = new LibraryBuilder("Light Novels", LibraryType.LightNovel).Build();

        UnitOfWork.LibraryRepository.Add(mangaLib);
        UnitOfWork.LibraryRepository.Add(lightNovelsLib);
        await UnitOfWork.CommitAsync();

        // Initially grant access to both libraries
        await accountService.UpdateLibrariesForUser(user, new List<int> { mangaLib.Id, lightNovelsLib.Id }, hasAdminRole: false);
        await UnitOfWork.CommitAsync();

        var userLibs = (await UnitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(user.Id)).ToList();
        Assert.Equal(2, userLibs.Count);

        // Now revoke all access by passing empty list
        await accountService.UpdateLibrariesForUser(user, new List<int>(), hasAdminRole: false);
        await UnitOfWork.CommitAsync();

        userLibs = (await UnitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(user.Id)).ToList();
        Assert.Empty(userLibs);
    }



    private async Task<(AppUser, IAccountService, UserManager<AppUser>, SettingsService)> Setup()
    {
        var defaultAdmin = new AppUserBuilder("defaultAdmin", "defaultAdmin@localhost")
            .WithRole(PolicyConstants.AdminRole)
            .Build();
        var user = new AppUserBuilder("amelia", "amelia@localhost").Build();

        var roleStore = new RoleStore<
            AppRole,
            DataContext,
            int,
            IdentityUserRole<int>,
            IdentityRoleClaim<int>
        >(Context);

        var roleManager = new RoleManager<AppRole>(
            roleStore,
            [new RoleValidator<AppRole>()],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            Substitute.For<ILogger<RoleManager<AppRole>>>());

        foreach (var role in PolicyConstants.ValidRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new AppRole
                {
                    Name = role,
                });
            }
        }

        var userStore = new UserStore<
            AppUser,
            AppRole,
            DataContext,
            int,
            IdentityUserClaim<int>,
            AppUserRole,
            IdentityUserLogin<int>,
            IdentityUserToken<int>,
            IdentityRoleClaim<int>
        >(Context);
        var userManager = new UserManager<AppUser>(userStore,
            new OptionsWrapper<IdentityOptions>(new IdentityOptions()),
            new PasswordHasher<AppUser>(),
            [new UserValidator<AppUser>()],
            [new PasswordValidator<AppUser>()],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            Substitute.For<ILogger<UserManager<AppUser>>>());

        // Create users with the UserManager such that the SecurityStamp is set
        await userManager.CreateAsync(user);
        await userManager.CreateAsync(defaultAdmin);

        var accountService = new AccountService(userManager, Substitute.For<ILogger<AccountService>>(), UnitOfWork, Mapper, Substitute.For<ILocalizationService>());
        var settingsService = new SettingsService(UnitOfWork, Substitute.For<IDirectoryService>(), Substitute.For<ILibraryWatcher>(), Substitute.For<ITaskScheduler>(), Substitute.For<ILogger<SettingsService>> (), Substitute.For<IOidcService>());

        user = await UnitOfWork.UserRepository.GetUserByIdAsync(user.Id, AppUserIncludes.SideNavStreams);
        return (user, accountService, userManager, settingsService);
    }

    protected override async Task ResetDb()
    {
        Context.AppUser.RemoveRange(Context.AppUser);
        Context.Library.RemoveRange(Context.Library);
        await UnitOfWork.CommitAsync();
    }
}
