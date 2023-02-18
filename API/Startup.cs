using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Logging;
using API.Middleware;
using API.Services;
using API.Services.HostedServices;
using API.Services.Tasks;
using API.SignalR;
using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.Storage.SQLite;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;
using Serilog;
using TaskScheduler = API.Services.TaskScheduler;

namespace API;

public class Startup
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public Startup(IConfiguration config, IWebHostEnvironment env)
    {
        _config = config;
        _env = env;
    }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddApplicationServices(_config, _env);

        services.AddControllers(options =>
        {
            options.CacheProfiles.Add(ResponseCacheProfiles.Instant,
                new CacheProfile()
                {
                    Duration = 30,
                    Location = ResponseCacheLocation.None,
                });
            options.CacheProfiles.Add(ResponseCacheProfiles.FiveMinute,
                new CacheProfile()
                {
                    Duration = 60 * 5,
                    Location = ResponseCacheLocation.None,
                });
            options.CacheProfiles.Add(ResponseCacheProfiles.TenMinute,
                new CacheProfile()
                {
                    Duration = 60 * 10,
                    Location = ResponseCacheLocation.None,
                    NoStore = false
                });
            options.CacheProfiles.Add(ResponseCacheProfiles.Hour,
                new CacheProfile()
                {
                    Duration = 60 * 60,
                    Location = ResponseCacheLocation.None,
                    NoStore = false
                });
            options.CacheProfiles.Add(ResponseCacheProfiles.Statistics,
                new CacheProfile()
                {
                    Duration = 60 * 60 * 6,
                    Location = ResponseCacheLocation.None,
                });
            options.CacheProfiles.Add(ResponseCacheProfiles.Images,
                new CacheProfile()
                {
                    Duration = 60,
                    Location = ResponseCacheLocation.None,
                    NoStore = false
                });
            options.CacheProfiles.Add(ResponseCacheProfiles.Month,
                new CacheProfile()
                {
                    Duration = TimeSpan.FromDays(30).Seconds,
                    Location = ResponseCacheLocation.Client,
                    NoStore = false
                });
        });
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.All;
            foreach(var proxy in _config.GetSection("KnownProxies").AsEnumerable().Where(c => c.Value != null)) {
                options.KnownProxies.Add(IPAddress.Parse(proxy.Value));
            }
        });
        services.AddCors();
        services.AddIdentityServices(_config);
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Version = BuildInfo.Version.ToString(),
                Title = "Kavita",
                Description = "Kavita provides a set of APIs that are authenticated by JWT. JWT token can be copied from local storage.",
                License = new OpenApiLicense
                {
                    Name = "GPL-3.0",
                    Url = new Uri("https://github.com/Kareadita/Kavita/blob/develop/LICENSE")
                }
            });

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var filePath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            c.IncludeXmlComments(filePath, true);
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme {
                In = ParameterLocation.Header,
                Description = "Please insert JWT with Bearer into field",
                Name = "Authorization",
                Type = SecuritySchemeType.ApiKey
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            c.AddServer(new OpenApiServer
            {
                Url = "{protocol}://{hostpath}",
                Variables = new Dictionary<string, OpenApiServerVariable>
                {
                    { "protocol", new OpenApiServerVariable { Default = "http", Enum = new List<string> { "http", "https" } } },
                    { "hostpath", new OpenApiServerVariable { Default = "localhost:5000" } }
                }
            });
        });
        services.AddResponseCompression(options =>
        {
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes =
                ResponseCompressionDefaults.MimeTypes.Concat(
                    new[] { "image/jpeg", "image/jpg" });
            options.EnableForHttps = true;
        });
        services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Fastest;
        });

        services.AddResponseCaching();

        services.AddHangfire(configuration => configuration
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseInMemoryStorage());
            //.UseSQLiteStorage("config/Hangfire.db")); // UseSQLiteStorage - SQLite has some issues around resuming jobs when aborted (and locking can cause high utilization)

        // Add the processing server as IHostedService
        services.AddHangfireServer(options =>
        {
            options.Queues = new[] {TaskScheduler.ScanQueue, TaskScheduler.DefaultQueue};
        });
        // Add IHostedService for startup tasks
        // Any services that should be bootstrapped go here
        services.AddHostedService<StartupTasksHostedService>();
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IBackgroundJobClient backgroundJobs, IWebHostEnvironment env,
        IHostApplicationLifetime applicationLifetime, IServiceProvider serviceProvider, ICacheService cacheService,
        IDirectoryService directoryService, IUnitOfWork unitOfWork, IBackupService backupService, IImageService imageService)
    {

        // Apply Migrations
        try
        {
            Task.Run(async () =>
                {
                    // Apply all migrations on startup
                    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                    var userManager = serviceProvider.GetRequiredService<UserManager<AppUser>>();
                    var themeService = serviceProvider.GetRequiredService<IThemeService>();
                    var dataContext = serviceProvider.GetRequiredService<DataContext>();
                    var readingListService = serviceProvider.GetRequiredService<IReadingListService>();


                    logger.LogInformation("Running Migrations");
                    // Only run this if we are upgrading
                    await MigrateChangePasswordRoles.Migrate(unitOfWork, userManager);
                    await MigrateRemoveExtraThemes.Migrate(unitOfWork, themeService);

                    // only needed for v0.5.4 and v0.6.0
                    await MigrateNormalizedEverything.Migrate(unitOfWork, dataContext, logger);

                    // v0.6.0
                    await MigrateChangeRestrictionRoles.Migrate(unitOfWork, userManager, logger);
                    await MigrateReadingListAgeRating.Migrate(unitOfWork, dataContext, readingListService, logger);

                    // v0.6.2 or v0.7
                    await MigrateSeriesRelationsImport.Migrate(dataContext, logger);

                    // v0.6.8 or v0.7
                    await MigrateUserProgressLibraryId.Migrate(unitOfWork, logger);
                    await MigrateToUtcDates.Migrate(unitOfWork, dataContext, logger);

                    //  Update the version in the DB after all migrations are run
                    var installVersion = await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.InstallVersion);
                    installVersion.Value = BuildInfo.Version.ToString();
                    unitOfWork.SettingsRepository.Update(installVersion);

                    await unitOfWork.CommitAsync();
                    logger.LogInformation("Running Migrations - done");
                }).GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogCritical(ex, "An error occurred during migration");
        }



        app.UseMiddleware<ExceptionMiddleware>();

        if (env.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Kavita API " + BuildInfo.Version);
            });
        }

        if (env.IsDevelopment())
        {
            app.UseHangfireDashboard();
        }

        app.UseResponseCompression();

        app.UseForwardedHeaders();

        app.UseRouting();

        // Ordering is important. Cors, authentication, authorization
        if (env.IsDevelopment())
        {
            app.UseCors(policy => policy
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials() // For SignalR token query param
                .WithOrigins("http://localhost:4200", $"http://{GetLocalIpAddress()}:4200", $"http://{GetLocalIpAddress()}:5000")
                .WithExposedHeaders("Content-Disposition", "Pagination"));
        }

        app.UseResponseCaching();

        app.UseAuthentication();

        app.UseAuthorization();

        app.UseDefaultFiles();

        app.UseStaticFiles(new StaticFileOptions
        {
            ContentTypeProvider = new FileExtensionContentTypeProvider(),
            HttpsCompression = HttpsCompressionMode.Compress,
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers[HeaderNames.CacheControl] = "public,max-age=" + TimeSpan.FromHours(24);
            }
        });

        app.UseSerilogRequestLogging(opts
            =>
        {
            opts.EnrichDiagnosticContext = LogEnricher.EnrichFromRequest;
        });

        app.Use(async (context, next) =>
        {
            context.Response.Headers[HeaderNames.Vary] =
                new[] { "Accept-Encoding" };

            // Don't let the site be iframed outside the same origin (clickjacking)
            context.Response.Headers.XFrameOptions = "SAMEORIGIN";

            // Setup CSP to ensure we load assets only from these origins
            context.Response.Headers.Add("Content-Security-Policy", "frame-ancestors 'none';");

            await next();
        });

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapHub<MessageHub>("hubs/messages");
            endpoints.MapHub<LogHub>("hubs/logs");
            endpoints.MapHangfireDashboard();
            endpoints.MapFallbackToController("Index", "Fallback");
        });

        applicationLifetime.ApplicationStopping.Register(OnShutdown);
        applicationLifetime.ApplicationStarted.Register(() =>
        {
            try
            {
                var logger = serviceProvider.GetRequiredService<ILogger<Startup>>();
                logger.LogInformation("Kavita - v{Version}", BuildInfo.Version);
            }
            catch (Exception)
            {
                /* Swallow Exception */
            }
            Console.WriteLine($"Kavita - v{BuildInfo.Version}");
        });
    }

    private static void OnShutdown()
    {
        Console.WriteLine("Server is shutting down. Please allow a few seconds to stop any background jobs...");
        TaskScheduler.Client.Dispose();
        System.Threading.Thread.Sleep(1000);
        Console.WriteLine("You may now close the application window.");
    }

    private static string GetLocalIpAddress()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
        socket.Connect("8.8.8.8", 65530);
        if (socket.LocalEndPoint is IPEndPoint endPoint) return endPoint.Address.ToString();
        throw new KavitaException("No network adapters with an IPv4 address in the system!");
    }

}
