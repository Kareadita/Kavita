using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.DTOs.Settings;
using API.Entities;
using API.Entities.Enums;
using API.Helpers.Builders;
using API.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;

public class OidcServiceTests: AbstractDbTest
{

    [Fact]
    public async Task SyncRoles()
    {
        await ResetDb();
        var (oidcService, user, _, _) = await Setup();

        var claims = new List<Claim>()
        {
            new (ClaimTypes.Role, PolicyConstants.LoginRole),
            new (ClaimTypes.Role, PolicyConstants.DownloadRole),
        };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        var settings = new OidcConfigDto
        {
            SyncUserSettings = true,
        };

        await oidcService.SyncUserSettings(settings, principal, user);

        var userRoles = await UnitOfWork.UserRepository.GetRoles(user.Id);
        Assert.Contains(PolicyConstants.LoginRole, userRoles);
        Assert.Contains(PolicyConstants.DownloadRole, userRoles);

        // Only give one role
        claims = [new Claim(ClaimTypes.Role, PolicyConstants.LoginRole)];
        identity = new ClaimsIdentity(claims);
        principal = new ClaimsPrincipal(identity);

        await oidcService.SyncUserSettings(settings, principal, user);

        userRoles = await UnitOfWork.UserRepository.GetRoles(user.Id);
        Assert.Contains(PolicyConstants.LoginRole, userRoles);
        Assert.DoesNotContain(PolicyConstants.DownloadRole, userRoles);
    }

    [Fact]
    public async Task SyncLibraries()
    {
        await ResetDb();
        var (oidcService, user, _, _) = await Setup();

        var mangaLib = new LibraryBuilder("Manga", LibraryType.Manga).Build();
        var lightNovelsLib = new LibraryBuilder("Light Novels", LibraryType.LightNovel).Build();

        UnitOfWork.LibraryRepository.Add(mangaLib);
        UnitOfWork.LibraryRepository.Add(lightNovelsLib);
        await UnitOfWork.CommitAsync();

        var claims = new List<Claim>()
        {
            new (ClaimTypes.Role, OidcService.LibraryAccessPrefix + "Manga"),
        };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        var settings = new OidcConfigDto
        {
            SyncUserSettings = true,
        };

        await oidcService.SyncUserSettings(settings, principal, user);

        var libraries = (await UnitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(user.Id)).Select(l => l.Name).ToList();
        Assert.Single(libraries);
        Assert.Contains(mangaLib.Name, libraries);
        Assert.DoesNotContain(lightNovelsLib.Name, libraries);

        // Only give access to the other library
        claims = [new Claim(ClaimTypes.Role, OidcService.LibraryAccessPrefix + "Light Novels")];
        identity = new ClaimsIdentity(claims);
        principal = new ClaimsPrincipal(identity);

        await oidcService.SyncUserSettings(settings, principal, user);

        // Check access has swicthed
        libraries = (await UnitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(user.Id)).Select(l => l.Name).ToList();
        Assert.Single(libraries);
        Assert.Contains(lightNovelsLib.Name, libraries);
        Assert.DoesNotContain(mangaLib.Name, libraries);
    }

    [Fact]
    public async Task SyncAgeRestrictions_IncludeUnknowns()
    {
        await ResetDb();
        var (oidcService, user, _, _) = await Setup();

        var claims = new List<Claim>()
        {
            new (ClaimTypes.Role, PolicyConstants.AdminRole),
            new (ClaimTypes.Role, OidcService.AgeRestrictionPrefix + "M"),
            new(ClaimTypes.Role, OidcService.IncludeUnknowns)
        };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        var settings = new OidcConfigDto
        {
            SyncUserSettings = true,
        };

        await oidcService.SyncUserSettings(settings, principal, user);

        var dbUser = await UnitOfWork.UserRepository.GetUserByIdAsync(user.Id);
        Assert.NotNull(dbUser);
        Assert.Equal(AgeRating.NotApplicable,  dbUser.AgeRestriction);
        Assert.True(dbUser.AgeRestrictionIncludeUnknowns);
    }

    [Fact]
    public async Task SyncAgeRestriction_AdminNone()
    {
        await ResetDb();
        var (oidcService, user, _, _) = await Setup();

        var claims = new List<Claim>()
        {
            new (ClaimTypes.Role, PolicyConstants.AdminRole),
            new (ClaimTypes.Role, OidcService.AgeRestrictionPrefix + "M"),
        };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        var settings = new OidcConfigDto
        {
            SyncUserSettings = true,
        };

        await oidcService.SyncUserSettings(settings, principal, user);

        var dbUser = await UnitOfWork.UserRepository.GetUserByIdAsync(user.Id);
        Assert.NotNull(dbUser);
        Assert.Equal(AgeRating.NotApplicable,  dbUser.AgeRestriction);
        Assert.True(dbUser.AgeRestrictionIncludeUnknowns);
    }

    [Fact]
    public async Task SyncAgeRestriction_MultipleAgeRestrictionClaims()
    {
        await ResetDb();
        var (oidcService, user, _, _) = await Setup();

        var claims = new List<Claim>()
        {
            new (ClaimTypes.Role, OidcService.AgeRestrictionPrefix + "Teen"),
            new (ClaimTypes.Role, OidcService.AgeRestrictionPrefix + "M"),
        };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        var settings = new OidcConfigDto
        {
            SyncUserSettings = true,
        };


        await oidcService.SyncUserSettings(settings, principal, user);

        var dbUser = await UnitOfWork.UserRepository.GetUserByIdAsync(user.Id);
        Assert.NotNull(dbUser);
        Assert.Equal(AgeRating.Mature,  dbUser.AgeRestriction);
    }

    [Fact]
    public async Task SyncUserSettings_DontChangeDefaultAdmin()
    {
        await ResetDb();
        var (oidcService, _, _, userManager) = await Setup();

        // Make user default user
        var user = await UnitOfWork.UserRepository.GetDefaultAdminUser();

        var settings = new OidcConfigDto
        {
            SyncUserSettings = true,
        };

        var claims = new List<Claim>()
        {
            new (ClaimTypes.Role, PolicyConstants.ChangePasswordRole),
            new (ClaimTypes.Role, OidcService.AgeRestrictionPrefix + "Teen"),
        };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        await oidcService.SyncUserSettings(settings, principal, user);

        var userFromDb = await UnitOfWork.UserRepository.GetUserByIdAsync(user.Id);
        Assert.NotNull(userFromDb);
        Assert.NotEqual(AgeRating.Teen, userFromDb.AgeRestriction);

        var newUser = new AppUserBuilder("NotAnAdmin", "NotAnAdmin@localhost").Build();
        var res = await userManager.CreateAsync(newUser);
        Assert.Empty(res.Errors);
        Assert.True(res.Succeeded);

        await oidcService.SyncUserSettings(settings, principal, newUser);
        userFromDb = await UnitOfWork.UserRepository.GetUserByIdAsync(newUser.Id);
        Assert.NotNull(userFromDb);
        Assert.True(await userManager.IsInRoleAsync(newUser, PolicyConstants.ChangePasswordRole));
        Assert.Equal(AgeRating.Teen, userFromDb.AgeRestriction);

    }

    [Fact]
    public async Task FindBestAvailableName_NoDuplicates()
    {
        await ResetDb();
        var (oidcService, _, _, userManager) = await Setup();


        const string preferredName = "PreferredName";
        const string name = "Name";
        const string givenName = "GivenName";
        const string surname = "Surname";
        const string email = "Email";

        var claims = new List<Claim>()
        {
            new(JwtRegisteredClaimNames.PreferredUsername, preferredName),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.GivenName, givenName),
            new(ClaimTypes.Surname, surname),
            new(ClaimTypes.Email, email),
        };

        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        var bestName = await oidcService.FindBestAvailableName(principal);
        Assert.NotNull(bestName);
        Assert.Equal(preferredName, bestName);

        // Create user with this name to make the method fallback to the next claim
        var user = new AppUserBuilder(bestName, bestName).Build();
        var res = await userManager.CreateAsync(user);
        // This has actual information as to why it would fail, so we check it to make sure if the test fail here we know why
        Assert.Empty(res.Errors);
        Assert.True(res.Succeeded);

        // Fallback to name
        bestName = await oidcService.FindBestAvailableName(principal);
        Assert.NotNull(bestName);
        Assert.Equal(name, bestName);

        user = new AppUserBuilder(bestName, bestName).Build();
        res = await userManager.CreateAsync(user);
        Assert.Empty(res.Errors);
        Assert.True(res.Succeeded);

        // Fallback to given name
        bestName = await oidcService.FindBestAvailableName(principal);
        Assert.NotNull(bestName);
        Assert.Equal(givenName, bestName);

        user = new AppUserBuilder(bestName, bestName).Build();
        res = await userManager.CreateAsync(user);
        Assert.Empty(res.Errors);
        Assert.True(res.Succeeded);

        // Fallback to surname
        bestName = await oidcService.FindBestAvailableName(principal);
        Assert.NotNull(bestName);
        Assert.Equal(surname, bestName);

        user = new AppUserBuilder(bestName, bestName).Build();
        res = await userManager.CreateAsync(user);
        Assert.Empty(res.Errors);
        Assert.True(res.Succeeded);

        // When none are found, returns null
        bestName = await oidcService.FindBestAvailableName(principal);
        Assert.Null(bestName);
    }

    private async Task<(OidcService, AppUser, IAccountService, UserManager<AppUser>)> Setup()
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
        var oidcService = new OidcService(Substitute.For<ILogger<OidcService>>(), userManager, UnitOfWork, accountService);
        return (oidcService, user, accountService, userManager);
    }

    protected override async Task ResetDb()
    {
        Context.AppUser.RemoveRange(Context.AppUser);
        Context.Library.RemoveRange(Context.Library);
        await UnitOfWork.CommitAsync();
    }
}
