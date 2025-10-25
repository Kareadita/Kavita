using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Entities;
using API.Services;
using Kavita.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using MessageReceivedContext = Microsoft.AspNetCore.Authentication.JwtBearer.MessageReceivedContext;
using TokenValidatedContext = Microsoft.AspNetCore.Authentication.OpenIdConnect.TokenValidatedContext;

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

        var auth = services.AddAuthentication(DynamicHybrid)
            .AddPolicyScheme(DynamicHybrid, JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var enabled = oidcSettings.Enabled;

                options.ForwardDefaultSelector = ctx =>
                {
                    if (!enabled) return LocalIdentity;

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


        if (oidcSettings.Enabled)
        {
            services.SetupOpenIdConnectAuthentication(auth, oidcSettings, environment);
        }

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
            .AddPolicy("RequireAdminRole", policy => policy.RequireRole(PolicyConstants.AdminRole))
            .AddPolicy("RequireDownloadRole", policy => policy.RequireRole(PolicyConstants.DownloadRole, PolicyConstants.AdminRole))
            .AddPolicy("RequireChangePasswordRole", policy => policy.RequireRole(PolicyConstants.ChangePasswordRole, PolicyConstants.AdminRole));

        return services;
    }

    private static void SetupOpenIdConnectAuthentication(this IServiceCollection services, AuthenticationBuilder auth,
        Configuration.OpenIdConnectSettings settings, IWebHostEnvironment environment)
    {
        var isDevelopment = environment.IsEnvironment(Environments.Development);
        var baseUrl = Configuration.BaseUrl;

        const string apiPrefix = "/api";
        const string hubsPrefix = "/hubs";

        var authority = Configuration.OidcSettings.Authority;
        if (!isDevelopment && !authority.StartsWith("https"))
        {
            Log.Error("OpenIdConnect authority is not using https, you must configure tls for your idp.");
            return;
        }

        var hasTrailingSlash = authority.EndsWith('/');
        var url = authority + (hasTrailingSlash ? string.Empty : "/") + ".well-known/openid-configuration";

        var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            url,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = !isDevelopment }
        );

        services.AddSingleton(configurationManager);

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
            // Do not interrupt startup if OIDC fails (Network outage should still allow Kavita to run)
            Log.Error(ex, "Failed to load OIDC configuration, OIDC will not be enabled. Restart to retry");
            return;
        }

        List<string> scopes = ["openid", "profile", "offline_access", "roles", "email"];
        scopes.AddRange(settings.CustomScopes);
        var validScopes = scopes.Where(scope =>
        {
            if (supportedScopes.Contains(scope))
                return true;

            Log.Warning("Scope {Scope} is configured, but not supported by your OIDC provider. Skipping", scope);
            return false;
        }).ToList();

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
                        var claims = await OidcService.ConstructNewClaimsList(ctx.HttpContext.RequestServices, ctx.Principal, user!, false);
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
            foreach (var scope in validScopes)
            {
                options.Scope.Add(scope);
            }


            options.Events = new OpenIdConnectEvents
            {
                OnTicketReceived = async ctx =>
                {
                    try
                    {
                        await OidcClaimsPrincipalConverter(ctx);
                    }
                    catch (KavitaException ex)
                    {
                        Log.Error(ex, "An exception occured during initial OIDC flow");
                        ctx.Response.Redirect(baseUrl + "login?skipAutoLogin=true&error=" + Uri.EscapeDataString(ex.Message));
                        ctx.HandleResponse();
                    }
                },
                OnUserInformationReceived = ctx =>
                {
                    if (ctx.Principal?.Identity == null)
                    {
                        return Task.CompletedTask;
                    }

                    var identity = (ClaimsIdentity) ctx.Principal.Identity;

                    // Copy all claims over as in, the ones we need mapped to something specific are above
                    foreach (var property in ctx.User.RootElement.EnumerateObject())
                    {
                        var claimType = property.Name;
                        if (property.Value.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var element in property.Value.EnumerateArray())
                            {
                                identity.AddClaim(new Claim(claimType, element.ToString(), ClaimValueTypes.String, OpenIdConnect));
                            }
                        }
                        else
                        {
                            identity.AddClaim(new Claim(claimType, property.Value.ToString(), ClaimValueTypes.String, OpenIdConnect));
                        }
                    }

                    return  Task.CompletedTask;
                },
                OnAuthenticationFailed = ctx =>
                {
                    ctx.Response.Redirect(baseUrl + "login?skipAutoLogin=true&error=" + Uri.EscapeDataString(ctx.Exception.Message));
                    ctx.HandleResponse();

                    return Task.CompletedTask;
                },
                OnRedirectToIdentityProviderForSignOut = ctx =>
                {
                    if (!isDevelopment && !string.IsNullOrEmpty(ctx.ProtocolMessage.PostLogoutRedirectUri))
                    {
                        ctx.ProtocolMessage.PostLogoutRedirectUri = ctx.ProtocolMessage.PostLogoutRedirectUri.Replace("http://", "https://");
                    }

                    return Task.CompletedTask;
                },
                OnRedirectToIdentityProvider = ctx =>
                {
                    // Intercept redirects on API requests and instead return 401
                    // These redirects are auto login when .NET finds a cookie that it can't match inside the cookie store. I.e. after a restart
                    if (ctx.Request.Path.StartsWithSegments(apiPrefix) || ctx.Request.Path.StartsWithSegments(hubsPrefix))
                    {
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        ctx.HandleResponse();
                        return Task.CompletedTask;
                    }

                    if (!isDevelopment && !string.IsNullOrEmpty(ctx.ProtocolMessage.RedirectUri))
                    {
                        ctx.ProtocolMessage.RedirectUri = ctx.ProtocolMessage.RedirectUri.Replace("http://", "https://");
                    }

                    return Task.CompletedTask;
                },
            };
        });
    }

    /// <summary>
    /// Called after the redirect from the OIDC provider, tries matching the user and update the principal
    /// to have the correct claims and properties. This is required to later auto refresh; and ensure .NET knows which
    /// Kavita roles the user has
    /// </summary>
    /// <param name="ctx"></param>
    private static async Task OidcClaimsPrincipalConverter(TicketReceivedContext ctx)
    {
        if (ctx.Principal == null) return;

        var oidcService = ctx.HttpContext.RequestServices.GetRequiredService<IOidcService>();
        var user = await oidcService.LoginOrCreate(ctx.Request, ctx.Principal);
        if (user == null)
        {
            throw new KavitaException("errors.oidc.no-account");
        }

        var claims = await OidcService.ConstructNewClaimsList(ctx.HttpContext.RequestServices, ctx.Principal, user);

        var identity = new ClaimsIdentity(claims, ctx.Scheme.Name);
        var principal = new ClaimsPrincipal(identity);

        ctx.HttpContext.User = principal;
        ctx.Principal = principal;

        ctx.Success();
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
