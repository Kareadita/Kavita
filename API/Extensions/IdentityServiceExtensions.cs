using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Entities;
using API.Helpers;
using API.Services;
using Kavita.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using MessageReceivedContext = Microsoft.AspNetCore.Authentication.JwtBearer.MessageReceivedContext;

namespace API.Extensions;
#nullable enable

public static class IdentityServiceExtensions
{
    private const string DynamicHybrid = nameof(DynamicHybrid);
    public const string OpenIdConnect = nameof(OpenIdConnect);
    private const string LocalIdentity = nameof(LocalIdentity);

    private const string OidcCallback = "/signin-oidc";
    private const string OidcLogoutCallback = "/signout-callback-oidc";

    public static IServiceCollection AddIdentityServices(this IServiceCollection services, IConfiguration config, IWebHostEnvironment environment)
    {
        services.Configure<IdentityOptions>(options =>
        {
            options.User.AllowedUserNameCharacters =
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+/";
        });

        services.AddIdentityCore<AppUser>(opt =>
            {
                opt.Password.RequireNonAlphanumeric = false;
                opt.Password.RequireDigit = false;
                opt.Password.RequireDigit = false;
                opt.Password.RequireLowercase = false;
                opt.Password.RequireUppercase = false;
                opt.Password.RequireNonAlphanumeric = false;
                opt.Password.RequiredLength = 6;

                opt.SignIn.RequireConfirmedEmail = false;

                opt.Lockout.AllowedForNewUsers = true;
                opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
                opt.Lockout.MaxFailedAccessAttempts = 5;

            })
            .AddTokenProvider<DataProtectorTokenProvider<AppUser>>(TokenOptions.DefaultProvider)
            .AddRoles<AppRole>()
            .AddRoleManager<RoleManager<AppRole>>()
            .AddSignInManager<SignInManager<AppUser>>()
            .AddRoleValidator<RoleValidator<AppRole>>()
            .AddEntityFrameworkStores<DataContext>();

        var oidcSettings = Configuration.OidcSettings;

        var auth = services.AddAuthentication(DynamicHybrid);
        var enableOidc = oidcSettings.Enabled && services.SetupOpenIdConnectAuthentication(auth, oidcSettings, environment);

        auth.AddPolicyScheme(DynamicHybrid, JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.ForwardDefaultSelector = ctx =>
            {
                if (!enableOidc) return LocalIdentity;

                if (ctx.Request.Path.StartsWithSegments(OidcCallback) ||
                    ctx.Request.Path.StartsWithSegments(OidcLogoutCallback))
                {
                    return OpenIdConnect;
                }

                if (ctx.Request.Headers.Authorization.Count != 0)
                {
                    return LocalIdentity;
                }

                if (ctx.Request.Cookies.ContainsKey(OidcService.CookieName))
                {
                    return OpenIdConnect;
                }

                return LocalIdentity;
            };
        });

        auth.AddJwtBearer(LocalIdentity, options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["TokenKey"]!)),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidIssuer = "Kavita",
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = SetTokenFromQuery,
            };
        });


        services.AddAuthorizationBuilder()
            .AddPolicy(PolicyGroups.AdminPolicy, policy => policy.RequireRole(PolicyConstants.AdminRole))
            .AddPolicy(PolicyGroups.DownloadPolicy,
                policy => policy.RequireRole(PolicyConstants.DownloadRole, PolicyConstants.AdminRole))
            .AddPolicy(PolicyGroups.ChangePasswordPolicy,
                policy => policy.RequireRole(PolicyConstants.ChangePasswordRole, PolicyConstants.AdminRole));

        return services;
    }

    private static bool SetupOpenIdConnectAuthentication(this IServiceCollection services, AuthenticationBuilder auth,
        Configuration.OpenIdConnectSettings settings, IWebHostEnvironment environment)
    {
        var isDevelopment = environment.IsEnvironment(Environments.Development);
        var baseUrl = Configuration.BaseUrl;

        var authority = Configuration.OidcSettings.Authority;
        if (!isDevelopment && !authority.StartsWith("https"))
        {
            Log.Error("OpenIdConnect authority is not using https, you must configure tls for your idp.");
            return false;
        }

        var hasTrailingSlash = authority.EndsWith('/');
        var url = authority + (hasTrailingSlash ? string.Empty : "/") + ".well-known/openid-configuration";

        var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            url,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = !isDevelopment }
        );

        services.AddSingleton(configurationManager);

        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme).Configure<ITicketStore>((options, store) =>
        {
            options.ExpireTimeSpan = TimeSpan.FromDays(7);
            options.SlidingExpiration = true;

            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.MaxAge = TimeSpan.FromDays(7);
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.SessionStore = store;

            if (isDevelopment)
            {
                options.Cookie.Domain = null;
            }

            options.Events = new CookieAuthenticationEvents
            {
                OnValidatePrincipal = async ctx =>
                {
                    var oidcService = ctx.HttpContext.RequestServices.GetRequiredService<IOidcService>();
                    var user = await oidcService.RefreshCookieToken(ctx);

                    if (user != null)
                    {
                        var claims = await OidcService.ConstructNewClaimsList(ctx.HttpContext.RequestServices, ctx.Principal, user, false);
                        ctx.ReplacePrincipal(new ClaimsPrincipal(new ClaimsIdentity(claims, ctx.Scheme.Name)));
                    }
                },
                OnRedirectToAccessDenied = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                },
            };
        });

        auth.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme);
        auth.AddOpenIdConnect(OpenIdConnect, options =>
        {
            options.Authority = settings.Authority;
            options.ClientId = settings.ClientId;
            options.ClientSecret = settings.Secret;
            options.RequireHttpsMetadata = options.Authority.StartsWith("https://");

            options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.CallbackPath = OidcCallback;
            options.SignedOutCallbackPath = OidcLogoutCallback;

            options.SaveTokens = true;
            options.GetClaimsFromUserInfoEndpoint = true;

            // Due to some (Authelia) OIDC providers, we need to map these claims explicitly. Such that no flow breaks in the
            // OidcService. Claims from the UserInfoEndPoint are not added automatically, we map some to the claim we need.
            // And copy all over down below
            options.MapInboundClaims = true;
            options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
            options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
            options.ClaimActions.MapJsonKey(ClaimTypes.GivenName, "given_name");

            options.Scope.Clear();
            foreach (var scope in GetValidScopes(configurationManager, settings))
            {
                options.Scope.Add(scope);
            }

            options.Events = new OpenIdConnectEventsHelper(baseUrl, isDevelopment);
        });

        return true;
    }

    private static IList<string> GetValidScopes(
        ConfigurationManager<OpenIdConnectConfiguration> configurationManager,
        Configuration.OpenIdConnectSettings settings
    )
    {
        var scopes = OidcService.DefaultScopes;
        scopes.AddRange(settings.CustomScopes);

        ICollection<string> supportedScopes;
        try
        {
            supportedScopes = configurationManager.GetConfigurationAsync()
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult()
                .ScopesSupported;
        }
        catch (Exception ex)
        {
            // Most idps will safely ignore invalid scopes (all except Authelia as far as I know), so we return them here
            // to have the least amount of impact on users
            Log.Error(ex, "Failed to load OIDC configuration, scopes will not be filtered. This may cause issues with some idps.");
            return scopes;
        }

        return scopes.Where(scope =>
        {
            if (supportedScopes.Contains(scope))
                return true;

            Log.Warning("Scope {Scope} is configured, but not supported by your OIDC provider. Skipping", scope);
            return false;
        }).ToList();
    }

    private static Task SetTokenFromQuery(MessageReceivedContext context)
    {
        var accessToken = context.Request.Query["access_token"];
        var path = context.HttpContext.Request.Path;

        // Only use query string based token on SignalR hubs
        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
        {
            context.Token = accessToken;
        }

        return Task.CompletedTask;
    }
}
