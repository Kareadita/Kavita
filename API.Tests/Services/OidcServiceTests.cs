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
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Polly;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services;

public class OidcServiceTests(ITestOutputHelper outputHelper): AbstractDbTest(outputHelper)
{

    [Fact]
    public async Task UserSync_Username()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (oidcService, _, _, userManager) = await Setup(unitOfWork, context, mapper);

        var user = new AppUserBuilder("holo", "holo@localhost")
            .WithIdentityProvider(IdentityProvider.OpenIdConnect)
            .Build();
        var res = await userManager.CreateAsync(user);
        Assert.Empty(res.Errors);
        Assert.True(res.Succeeded);

        var claims = new List<Claim>()
        {
            new (ClaimTypes.Name, "amelia"),
            new (ClaimTypes.GivenName, "Lawrence"),
        };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        var settings = new OidcConfigDto
        {
            SyncUserSettings = true,
        };

        // name is updated as the current username is not found, amelia is skipped as it is alredy in use
        await oidcService.SyncUserSettings(null!, settings, principal, user);
        var dbUser = await unitOfWork.UserRepository.GetUserByIdAsync(user.Id);
        Assert.NotNull(dbUser);
        Assert.Equal("Lawrence", user.UserName);

        claims = new List<Claim>()
        {
            new (ClaimTypes.Name, "amelia"),
            new (ClaimTypes.GivenName, "Lawrence"),
            new (ClaimTypes.Surname, "Norah"),
        };
        identity = new ClaimsIdentity(claims);
        principal = new ClaimsPrincipal(identity);

        // Ensure a name longer down the list isn't picked if the current username is found
        await oidcService.SyncUserSettings(null!, settings, principal, user);
        dbUser = await unitOfWork.UserRepository.GetUserByIdAsync(user.Id);
        Assert.NotNull(dbUser);
        Assert.Equal("Lawrence", user.UserName);
    }

    [Fact]
    public async Task UserSync_CustomClaim()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (oidcService, user, _, _) = await Setup(unitOfWork, context, mapper);

        var mangaLib = new LibraryBuilder("Manga", LibraryType.Manga).Build();
        var lightNovelsLib = new LibraryBuilder("Light Novels", LibraryType.LightNovel).Build();

        unitOfWork.LibraryRepository.Add(mangaLib);
        unitOfWork.LibraryRepository.Add(lightNovelsLib);
        await unitOfWork.CommitAsync();

        const string claim = "groups";
        var claims = new List<Claim>()
        {
            new (claim, PolicyConstants.LoginRole),
            new (claim, PolicyConstants.DownloadRole),
            new (ClaimTypes.Role, PolicyConstants.PromoteRole),
            new (claim, OidcService.AgeRestrictionPrefix + "M"),
            new (claim, OidcService.LibraryAccessPrefix + "Manga"),
            new (ClaimTypes.Role, OidcService.LibraryAccessPrefix + "Light Novels"),
        };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        var settings = new OidcConfigDto
        {
            SyncUserSettings = true,
            RolesClaim = claim,
        };

        await oidcService.SyncUserSettings(null!, settings, principal, user);

        // Check correct roles assigned
        var userRoles = await unitOfWork.UserRepository.GetRoles(user.Id);
        Assert.Contains(PolicyConstants.LoginRole, userRoles);
        Assert.Contains(PolicyConstants.DownloadRole, userRoles);
        Assert.DoesNotContain(PolicyConstants.PromoteRole, userRoles);

        // Check correct libraries
        var libraries = (await unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(user.Id)).Select(l => l.Name).ToList();
        Assert.Single(libraries);
        Assert.Contains(mangaLib.Name, libraries);
        Assert.DoesNotContain(lightNovelsLib.Name, libraries);

        // Check correct age restrictions
        var dbUser = await unitOfWork.UserRepository.GetUserByIdAsync(user.Id);
        Assert.NotNull(dbUser);
        Assert.Equal(AgeRating.Mature,  dbUser.AgeRestriction);
        Assert.False(dbUser.AgeRestrictionIncludeUnknowns);
    }

    [Fact]
    public async Task UserSync_CustomPrefix()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (oidcService, user, _, _) = await Setup(unitOfWork, context, mapper);

        var mangaLib = new LibraryBuilder("Manga", LibraryType.Manga).Build();
        var lightNovelsLib = new LibraryBuilder("Light Novels", LibraryType.LightNovel).Build();

        unitOfWork.LibraryRepository.Add(mangaLib);
        unitOfWork.LibraryRepository.Add(lightNovelsLib);
        await unitOfWork.CommitAsync();

        const string prefix = "kavita-";
        var claims = new List<Claim>()
        {
            new (ClaimTypes.Role, prefix + PolicyConstants.LoginRole),
            new (ClaimTypes.Role, prefix + PolicyConstants.DownloadRole),
            new (ClaimTypes.Role, PolicyConstants.PromoteRole),
            new (ClaimTypes.Role, prefix + OidcService.AgeRestrictionPrefix + "M"),
            new (ClaimTypes.Role, prefix + OidcService.LibraryAccessPrefix + "Manga"),
            new (ClaimTypes.Role, OidcService.LibraryAccessPrefix + "Light Novels"),
        };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        var settings = new OidcConfigDto
        {
            SyncUserSettings = true,
            RolesPrefix = prefix,
        };

        await oidcService.SyncUserSettings(null!, settings, principal, user);

        // Check correct roles assigned
        var userRoles = await unitOfWork.UserRepository.GetRoles(user.Id);
        Assert.Contains(PolicyConstants.LoginRole, userRoles);
        Assert.Contains(PolicyConstants.DownloadRole, userRoles);
        Assert.DoesNotContain(PolicyConstants.PromoteRole, userRoles);

        // Check correct libraries
        var libraries = (await unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(user.Id)).Select(l => l.Name).ToList();
        Assert.Single(libraries);
        Assert.Contains(mangaLib.Name, libraries);
        Assert.DoesNotContain(lightNovelsLib.Name, libraries);

        // Check correct age restrictions
        var dbUser = await unitOfWork.UserRepository.GetUserByIdAsync(user.Id);
        Assert.NotNull(dbUser);
        Assert.Equal(AgeRating.Mature,  dbUser.AgeRestriction);
        Assert.False(dbUser.AgeRestrictionIncludeUnknowns);
    }

    [Fact]
    public async Task SyncRoles()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (oidcService, user, _, _) = await Setup(unitOfWork, context, mapper);

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

        await oidcService.SyncUserSettings(null!, settings, principal, user);

        var userRoles = await unitOfWork.UserRepository.GetRoles(user.Id);
        Assert.Contains(PolicyConstants.LoginRole, userRoles);
        Assert.Contains(PolicyConstants.DownloadRole, userRoles);

        // Only give one role
        claims = [new Claim(ClaimTypes.Role, PolicyConstants.LoginRole)];
        identity = new ClaimsIdentity(claims);
        principal = new ClaimsPrincipal(identity);

        await oidcService.SyncUserSettings(null!, settings, principal, user);

        userRoles = await unitOfWork.UserRepository.GetRoles(user.Id);
        Assert.Contains(PolicyConstants.LoginRole, userRoles);
        Assert.DoesNotContain(PolicyConstants.DownloadRole, userRoles);
    }

    [Fact]
    public async Task SyncLibraries()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (oidcService, user, _, _) = await Setup(unitOfWork, context, mapper);

        var mangaLib = new LibraryBuilder("Manga", LibraryType.Manga).Build();
        var lightNovelsLib = new LibraryBuilder("Light Novels", LibraryType.LightNovel).Build();

        unitOfWork.LibraryRepository.Add(mangaLib);
        unitOfWork.LibraryRepository.Add(lightNovelsLib);
        await unitOfWork.CommitAsync();

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

        await oidcService.SyncUserSettings(null!, settings, principal, user);

        var libraries = (await unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(user.Id)).Select(l => l.Name).ToList();
        Assert.Single(libraries);
        Assert.Contains(mangaLib.Name, libraries);
        Assert.DoesNotContain(lightNovelsLib.Name, libraries);

        // Only give access to the other library
        claims = [new Claim(ClaimTypes.Role, OidcService.LibraryAccessPrefix + "Light Novels")];
        identity = new ClaimsIdentity(claims);
        principal = new ClaimsPrincipal(identity);

        await oidcService.SyncUserSettings(null!, settings, principal, user);

        // Check access has swicthed
        libraries = (await unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(user.Id)).Select(l => l.Name).ToList();
        Assert.Single(libraries);
        Assert.Contains(lightNovelsLib.Name, libraries);
        Assert.DoesNotContain(mangaLib.Name, libraries);
    }

    [Fact]
    public async Task SyncAgeRestrictions_NoRestrictions()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (oidcService, user, _, _) = await Setup(unitOfWork, context, mapper);

        var claims = new List<Claim>()
        {
            new (ClaimTypes.Role, OidcService.AgeRestrictionPrefix + "Not Applicable"),
            new(ClaimTypes.Role, OidcService.AgeRestrictionPrefix + OidcService.IncludeUnknowns),
        };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        var settings = new OidcConfigDto
        {
            SyncUserSettings = true,
        };

        await oidcService.SyncUserSettings(null!, settings, principal, user);

        var dbUser = await unitOfWork.UserRepository.GetUserByIdAsync(user.Id);
        Assert.NotNull(dbUser);
        Assert.Equal(AgeRating.NotApplicable,  dbUser.AgeRestriction);
        Assert.True(dbUser.AgeRestrictionIncludeUnknowns);
    }

    [Fact]
    public async Task SyncAgeRestrictions_IncludeUnknowns()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (oidcService, user, _, _) = await Setup(unitOfWork, context, mapper);

        var claims = new List<Claim>()
        {
            new (ClaimTypes.Role, OidcService.AgeRestrictionPrefix + "M"),
            new(ClaimTypes.Role, OidcService.AgeRestrictionPrefix + OidcService.IncludeUnknowns),
        };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        var settings = new OidcConfigDto
        {
            SyncUserSettings = true,
        };

        await oidcService.SyncUserSettings(null!, settings, principal, user);

        var dbUser = await unitOfWork.UserRepository.GetUserByIdAsync(user.Id);
        Assert.NotNull(dbUser);
        Assert.Equal(AgeRating.Mature,  dbUser.AgeRestriction);
        Assert.True(dbUser.AgeRestrictionIncludeUnknowns);
    }

    [Fact]
    public async Task SyncAgeRestriction_AdminNone()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (oidcService, user, _, _) = await Setup(unitOfWork, context, mapper);

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

        await oidcService.SyncUserSettings(null!, settings, principal, user);

        var dbUser = await unitOfWork.UserRepository.GetUserByIdAsync(user.Id);
        Assert.NotNull(dbUser);
        Assert.Equal(AgeRating.NotApplicable,  dbUser.AgeRestriction);
        Assert.True(dbUser.AgeRestrictionIncludeUnknowns);
    }

    [Fact]
    public async Task SyncAgeRestriction_MultipleAgeRestrictionClaims()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (oidcService, user, _, _) = await Setup(unitOfWork, context, mapper);

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


        await oidcService.SyncUserSettings(null!, settings, principal, user);

        var dbUser = await unitOfWork.UserRepository.GetUserByIdAsync(user.Id);
        Assert.NotNull(dbUser);
        Assert.Equal(AgeRating.Mature,  dbUser.AgeRestriction);
    }

    [Fact]
    public async Task SyncAgeRestriction_NoAgeRestrictionClaims()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (oidcService, user, _, _) = await Setup(unitOfWork, context, mapper);

        var identity = new ClaimsIdentity([]);
        var principal = new ClaimsPrincipal(identity);

        var settings = new OidcConfigDto
        {
            SyncUserSettings = true,
        };

        await oidcService.SyncUserSettings(null!, settings, principal, user);

        var dbUser = await unitOfWork.UserRepository.GetUserByIdAsync(user.Id);
        Assert.NotNull(dbUser);
        Assert.Equal(AgeRating.NotApplicable,  dbUser.AgeRestriction);
        Assert.True(dbUser.AgeRestrictionIncludeUnknowns);

        // Also default to no restrictions when only include unknowns is present
        identity = new ClaimsIdentity([new Claim(ClaimTypes.Role, OidcService.AgeRestrictionPrefix + OidcService.IncludeUnknowns)]);
        principal = new ClaimsPrincipal(identity);

        await oidcService.SyncUserSettings(null!, settings, principal, user);

        dbUser = await unitOfWork.UserRepository.GetUserByIdAsync(user.Id);
        Assert.NotNull(dbUser);
        Assert.Equal(AgeRating.NotApplicable,  dbUser.AgeRestriction);
        Assert.True(dbUser.AgeRestrictionIncludeUnknowns);
    }

    [Fact]
    public async Task SyncUserSettings_DontChangeDefaultAdmin()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (oidcService, _, _, userManager) = await Setup(unitOfWork, context, mapper);

        // Make user default user
        var user = await unitOfWork.UserRepository.GetDefaultAdminUser();

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

        await oidcService.SyncUserSettings(null!, settings, principal, user);

        var userFromDb = await unitOfWork.UserRepository.GetUserByIdAsync(user.Id);
        Assert.NotNull(userFromDb);
        Assert.NotEqual(AgeRating.Teen, userFromDb.AgeRestriction);

        var newUser = new AppUserBuilder("NotAnAdmin", "NotAnAdmin@localhost")
            .WithIdentityProvider(IdentityProvider.OpenIdConnect)
            .Build();
        var res = await userManager.CreateAsync(newUser);
        Assert.Empty(res.Errors);
        Assert.True(res.Succeeded);

        await oidcService.SyncUserSettings(null!, settings, principal, newUser);
        userFromDb = await unitOfWork.UserRepository.GetUserByIdAsync(newUser.Id);
        Assert.NotNull(userFromDb);
        Assert.True(await userManager.IsInRoleAsync(newUser, PolicyConstants.ChangePasswordRole));
        Assert.Equal(AgeRating.Teen, userFromDb.AgeRestriction);

    }

    [Fact]
    public async Task FindBestAvailableName_NoDuplicates()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (oidcService, _, _, userManager) = await Setup(unitOfWork, context, mapper);


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

    private async Task<(OidcService, AppUser, IAccountService, UserManager<AppUser>)> Setup(IUnitOfWork unitOfWork, DataContext context, IMapper mapper)
    {
        // Remove the default library created with the AbstractDbTest class
        context.Library.RemoveRange(context.Library);
        await context.SaveChangesAsync();

        var defaultAdmin = new AppUserBuilder("defaultAdmin", "defaultAdmin@localhost")
            .WithRole(PolicyConstants.AdminRole)
            .Build();
        var user = new AppUserBuilder("amelia", "amelia@localhost")
            .WithIdentityProvider(IdentityProvider.OpenIdConnect)
            .Build();

        var roleStore = new RoleStore<
            AppRole,
            DataContext,
            int,
            IdentityUserRole<int>,
            IdentityRoleClaim<int>
        >(context);

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
        >(context);
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

        var accountService = new AccountService(userManager, Substitute.For<ILogger<AccountService>>(),
            unitOfWork, mapper, Substitute.For<ILocalizationService>());
        var oidcService = new OidcService(Substitute.For<ILogger<OidcService>>(), userManager, unitOfWork,
            accountService, Substitute.For<IEmailService>());

        return (oidcService, user, accountService, userManager);
    }
}
