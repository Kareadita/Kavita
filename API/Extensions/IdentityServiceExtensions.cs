using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
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
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MessageReceivedContext = Microsoft.AspNetCore.Authentication.JwtBearer.MessageReceivedContext;
using TokenValidatedContext = Microsoft.AspNetCore.Authentication.JwtBearer.TokenValidatedContext;

namespace API.Extensions;
#nullable enable

public static class IdentityServiceExtensions
{
    private const string DynamicJwt = nameof(DynamicJwt);
    private const string OpenIdConnect = nameof(OpenIdConnect);
    private const string LocalIdentity = nameof(LocalIdentity);

    public static IServiceCollection AddIdentityServices(this IServiceCollection services, IConfiguration config)
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

        var auth = services.AddAuthentication(DynamicJwt)
            .AddPolicyScheme(DynamicJwt, JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var iss = Configuration.OidcAuthority;
                var enabled = Configuration.OidcEnabled;
                options.ForwardDefaultSelector = context =>
                {
                    if (!enabled)
                        return LocalIdentity;

                    var fullAuth =
                        context.Request.Headers["Authorization"].FirstOrDefault() ??
                        context.Request.Query["access_token"].FirstOrDefault();

                    var token = fullAuth?.TrimPrefix("Bearer ");

                    if (string.IsNullOrEmpty(token))
                        return LocalIdentity;

                    var handler = new JwtSecurityTokenHandler();
                    try
                    {
                        var jwt = handler.ReadJwtToken(token);
                        if (jwt.Issuer == iss) return OpenIdConnect;
                    }
                    catch
                    {
                        /* Swallow */
                    }

                    return LocalIdentity;
                };
            });

        if (Configuration.OidcEnabled)
        {
            services.AddScoped<IClaimsTransformation, RolesClaimsTransformation>();

            // TODO: Investigate on how to make this not hardcoded at startup
            auth.AddJwtBearer(OpenIdConnect, options =>
            {
                options.Authority = Configuration.OidcAuthority;
                options.Audience = Configuration.OidcClientId;
                options.RequireHttpsMetadata = options.Authority.StartsWith("https://");

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidAudience = Configuration.OidcClientId,
                    ValidIssuer = Configuration.OidcAuthority,

                    ValidateIssuer = true,
                    ValidateAudience = true,

                    ValidateIssuerSigningKey = true,
                    RequireExpirationTime = true,
                    ValidateLifetime = true,
                    RequireSignedTokens = true
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = SetTokenFromQuery,
                    OnTokenValidated = OidcClaimsPrincipalConverter
                };
            });
        }

        auth.AddJwtBearer(LocalIdentity, options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["TokenKey"]!)),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidIssuer = "Kavita"
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = SetTokenFromQuery
            };
        });


        services.AddAuthorization(opt =>
        {
            opt.AddPolicy("RequireAdminRole", policy => policy.RequireRole(PolicyConstants.AdminRole));
            opt.AddPolicy("RequireDownloadRole",
                policy => policy.RequireRole(PolicyConstants.DownloadRole, PolicyConstants.AdminRole));
            opt.AddPolicy("RequireChangePasswordRole",
                policy => policy.RequireRole(PolicyConstants.ChangePasswordRole, PolicyConstants.AdminRole));
        });

        return services;
    }

    private static async Task OidcClaimsPrincipalConverter(TokenValidatedContext ctx)
    {
        var oidcService = ctx.HttpContext.RequestServices.GetRequiredService<IOidcService>();
        if (ctx.Principal == null) return;

        var user = await oidcService.LoginOrCreate(ctx.Principal);
        if (user == null) return;


        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.Name, user.UserName ?? string.Empty)
        };

        var userManager = ctx.HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();
        var roles = await userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(ctx.Principal.Claims);

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
        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs")) context.Token = accessToken;

        return Task.CompletedTask;
    }
}
