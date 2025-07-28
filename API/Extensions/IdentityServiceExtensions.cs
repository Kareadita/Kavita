using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
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
using Microsoft.Extensions.Hosting;using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using MessageReceivedContext = Microsoft.AspNetCore.Authentication.JwtBearer.MessageReceivedContext;
using TokenValidatedContext = Microsoft.AspNetCore.Authentication.OpenIdConnect.TokenValidatedContext;

namespace API.Extensions;
#nullable enable

public static class IdentityServiceExtensions
{
    private const string DynamicHybrid = nameof(DynamicHybrid);
    public const string OpenIdConnect = nameof(OpenIdConnect);
    private const string LocalIdentity = nameof(LocalIdentity);

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

                    if (ctx.Request.Path.StartsWithSegments("/signin-oidc") ||
                        ctx.Request.Path.StartsWithSegments("/signout-callback-oidc"))
                    {
                        return OpenIdConnect;
                    }

                    if (ctx.Request.Cookies.ContainsKey(".AspNetCore.Cookies"))
                    {
                        return OpenIdConnect;
                    }

                    return LocalIdentity;
                };

            });


        if (oidcSettings.Enabled)
        {
            auth.AddOpenIdConnectCookie(environment)
                .AddOpenIdConnectScheme(oidcSettings);
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

    private static AuthenticationBuilder AddOpenIdConnectScheme(this AuthenticationBuilder builder, Configuration.OpenIdConnectSettings settings)
    {
        return builder.AddOpenIdConnect(OpenIdConnect, options =>
        {
            options.Authority = settings.Authority;
            options.ClientId = settings.ClientId;
            options.ClientSecret = settings.Secret;
            options.RequireHttpsMetadata = options.Authority.StartsWith("https://");

            options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.CallbackPath = "/signin-oidc";
            options.SignedOutCallbackPath = "/signout-callback-oidc";

            options.SaveTokens = true;
            options.GetClaimsFromUserInfoEndpoint = true;
            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("offline_access");
            options.Scope.Add("roles");
            options.Scope.Add("email");
            foreach (var customScope in settings.CustomScopes)
            {
                options.Scope.Add(customScope);
            }

            options.Events = new OpenIdConnectEvents
            {
                OnTokenValidated = OidcClaimsPrincipalConverter,
                OnAuthenticationFailed = ctx =>
                {
                    ctx.Response.Redirect("/login?skipAutoLogin=true&error="+Uri.EscapeDataString(ctx.Exception.Message));
                    ctx.HandleResponse();
                    return Task.CompletedTask;
                },
            };
        });
    }

    private static AuthenticationBuilder AddOpenIdConnectCookie(this AuthenticationBuilder builder, IWebHostEnvironment environment)
    {
        return builder.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.ExpireTimeSpan = TimeSpan.FromDays(7);
            options.SlidingExpiration = true;

            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.MaxAge = TimeSpan.FromDays(7);

            if (environment.IsEnvironment(Environments.Development))
            {
                options.Cookie.Domain = null;
            }

            options.Events = new CookieAuthenticationEvents
            {
                OnValidatePrincipal = async ctx =>
                {
                    var oidcService = ctx.HttpContext.RequestServices.GetRequiredService<IOidcService>();
                    await oidcService.RefreshCookieToken(ctx);
                },
                OnRedirectToAccessDenied = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                },
                OnRedirectToLogin = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                },
            };
        });
    }

    /// <summary>
    /// Called after the OIDC token has been validated, only called on login. Used to find the user we'll be authenticating against
    /// </summary>
    /// <param name="ctx"></param>
    private static async Task OidcClaimsPrincipalConverter(TokenValidatedContext ctx)
    {
        if (ctx.Principal == null) return;

        var oidcService = ctx.HttpContext.RequestServices.GetRequiredService<IOidcService>();
        var user = await oidcService.LoginOrCreate(ctx.Request, ctx.Principal);
        if (user == null)
        {
            throw new KavitaException("errors.oidc.no-account");
        }

        var claims = await OidcService.ConstructNewClaimsList(ctx.HttpContext.RequestServices, ctx.Principal, user);
        var tokens = CopyAuthenticationTokens(ctx);

        var identity = new ClaimsIdentity(claims, ctx.Scheme.Name);
        var principal = new ClaimsPrincipal(identity);

        ctx.Properties ??= new AuthenticationProperties();
        ctx.Properties.StoreTokens(tokens);

        ctx.HttpContext.User = principal;
        ctx.Principal = principal;

        ctx.Success();
    }

    private static List<AuthenticationToken> CopyAuthenticationTokens(TokenValidatedContext ctx)
    {
        var tokens = new List<AuthenticationToken>
        {
            new() {Name = OidcService.IdToken, Value = ctx.SecurityToken.RawData},
        };

        if (ctx.TokenEndpointResponse == null)
        {
            return tokens;
        }

        if (!string.IsNullOrEmpty(ctx.TokenEndpointResponse.AccessToken))
        {
            tokens.Add(new AuthenticationToken { Name = OidcService.AccessToken, Value = ctx.TokenEndpointResponse.AccessToken });
        }

        if (!string.IsNullOrEmpty(ctx.TokenEndpointResponse.RefreshToken))
        {
            tokens.Add(new AuthenticationToken { Name = OidcService.RefreshToken, Value = ctx.TokenEndpointResponse.RefreshToken });
        }

        if (!string.IsNullOrEmpty(ctx.TokenEndpointResponse.ExpiresIn))
        {
            var expiresAt = DateTime.UtcNow.AddSeconds(double.Parse(ctx.TokenEndpointResponse.ExpiresIn));
            tokens.Add(new AuthenticationToken { Name = OidcService.ExpiresAt, Value = expiresAt.ToString("o") });
        }

        return tokens;
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
