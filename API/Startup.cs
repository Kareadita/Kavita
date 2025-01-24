using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.ManualMigrations;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Logging;
using API.Middleware;
using API.Middleware.RateLimit;
using API.Services;
using API.Services.HostedServices;
using API.Services.Tasks;
using API.SignalR;
using Hangfire;
using HtmlAgilityPack;
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
using Microsoft.EntityFrameworkCore;
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
            options.CacheProfiles.Add(ResponseCacheProfiles.LicenseCache,
                new CacheProfile()
                {
                    Duration = TimeSpan.FromHours(4).Seconds,
                    Location = ResponseCacheLocation.Client,
                    NoStore = false
                });
            options.CacheProfiles.Add(ResponseCacheProfiles.KavitaPlus,
                new CacheProfile()
                {
                    Duration = TimeSpan.FromDays(30).Seconds,
                    Location = ResponseCacheLocation.Any,
                    NoStore = false
                });
        });
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.All;
            foreach(var proxy in _config.GetSection("KnownProxies").AsEnumerable().Where(c => c.Value != null)) {
                options.KnownProxies.Add(IPAddress.Parse(proxy.Value!));
            }
        });
        services.AddCors();
        services.AddIdentityServices(_config);
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Version = "3.1.0",
                Title = "Kavita",
                Description = $"Kavita provides a set of APIs that are authenticated by JWT. JWT token can be copied from local storage. Assume all fields of a payload are required. Built against v{BuildInfo.Version.ToString()}",
                License = new OpenApiLicense
                {
                    Name = "GPL-3.0",
                    Url = new Uri("https://github.com/Kareadita/Kavita/blob/develop/LICENSE")
                },
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
                    { "protocol", new OpenApiServerVariable { Default = "http", Enum = ["http", "https"]} },
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
                    new[] { "image/jpeg", "image/jpg", "image/png", "image/avif", "image/gif", "image/webp", "image/tiff" });
            options.EnableForHttps = true;
        });
        services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Fastest;
        });

        services.AddResponseCaching();

        services.AddRateLimiter(options =>
        {
            options.AddPolicy("Authentication", httpContext =>
                new AuthenticationRateLimiterPolicy().GetPartition(httpContext));
        });

        services.AddHangfire(configuration => configuration
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseInMemoryStorage());
            //.UseSQLiteStorage("config/Hangfire.db")); // UseSQLiteStorage - SQLite has some issues around resuming jobs when aborted (and locking can cause high utilization) (NOTE: There is code to clear jobs on startup a redditor gave me)

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

        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        // Apply Migrations
        try
        {
            Task.Run(async () =>
                {
                    // Apply all migrations on startup
                    var dataContext = serviceProvider.GetRequiredService<DataContext>();


                    logger.LogInformation("Running Migrations");

                    // v0.7.9
                    await MigrateUserLibrarySideNavStream.Migrate(unitOfWork, dataContext, logger);

                    // v0.7.11
                    await MigrateSmartFilterEncoding.Migrate(unitOfWork, dataContext, logger);
                    await MigrateLibrariesToHaveAllFileTypes.Migrate(unitOfWork, dataContext, logger);


                    // v0.7.14
                    await MigrateEmailTemplates.Migrate(directoryService, logger);
                    await MigrateVolumeNumber.Migrate(dataContext, logger);
                    await MigrateWantToReadImport.Migrate(unitOfWork, dataContext, directoryService, logger);
                    await MigrateManualHistory.Migrate(dataContext, logger);
                    await MigrateClearNightlyExternalSeriesRecords.Migrate(dataContext, logger);

                    // v0.8.0
                    await MigrateVolumeLookupName.Migrate(dataContext, unitOfWork, logger);
                    await MigrateChapterNumber.Migrate(dataContext, logger);
                    await MigrateProgressExport.Migrate(dataContext, directoryService, logger);
                    await MigrateMixedSpecials.Migrate(dataContext, unitOfWork, directoryService, logger);
                    await MigrateLooseLeafChapters.Migrate(dataContext, unitOfWork, directoryService, logger);
                    await MigrateChapterFields.Migrate(dataContext, unitOfWork, logger);
                    await MigrateChapterRange.Migrate(dataContext, unitOfWork, logger);
                    await MigrateMangaFilePath.Migrate(dataContext, logger);
                    await MigrateCollectionTagToUserCollections.Migrate(dataContext, unitOfWork, logger);

                    // v0.8.1
                    await MigrateLowestSeriesFolderPath.Migrate(dataContext, unitOfWork, logger);

                    // v0.8.2
                    await ManualMigrateThemeDescription.Migrate(dataContext, logger);
                    await MigrateInitialInstallData.Migrate(dataContext, logger, directoryService);
                    await MigrateSeriesLowestFolderPath.Migrate(dataContext, logger, directoryService);

                    // v0.8.4
                    await MigrateLowestSeriesFolderPath2.Migrate(dataContext, unitOfWork, logger);
                    await ManualMigrateRemovePeople.Migrate(dataContext, logger);
                    await MigrateDuplicateDarkTheme.Migrate(dataContext, logger);
                    await ManualMigrateUnscrobbleBookLibraries.Migrate(dataContext, logger);

                    // v0.8.5
                    await ManualMigrateBlacklistTableToSeries.Migrate(dataContext, logger);
                    await ManualMigrateInvalidBlacklistSeries.Migrate(dataContext, logger);

                    //  Update the version in the DB after all migrations are run
                    var installVersion = await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.InstallVersion);
                    installVersion.Value = BuildInfo.Version.ToString();
                    unitOfWork.SettingsRepository.Update(installVersion);
                    await unitOfWork.CommitAsync();

                    logger.LogInformation("Running Migrations - complete");
                }).GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "An error occurred during migration");
        }

        app.UseMiddleware<ExceptionMiddleware>();
        app.UseMiddleware<SecurityEventMiddleware>();


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

        app.UseRateLimiter();

        var basePath = Configuration.BaseUrl;
        app.UsePathBase(basePath);
        if (!env.IsDevelopment())
        {
            // We don't update the index.html in local as we don't serve from there
            UpdateBaseUrlInIndex(basePath);

            // Update DB with what's in config
            var dataContext = serviceProvider.GetRequiredService<DataContext>();
            var setting = dataContext.ServerSetting.SingleOrDefault(x => x.Key == ServerSettingKey.BaseUrl);
            if (setting != null)
            {
                setting.Value = basePath;
            }

            dataContext.SaveChanges();
        }

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
        else
        {
            // Allow CORS for Kavita's url
            app.UseCors(policy => policy
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials() // For SignalR token query param
                .WithExposedHeaders("Content-Disposition", "Pagination"));
        }

        app.UseResponseCaching();

        app.UseAuthentication();

        app.UseAuthorization();

        app.UseDefaultFiles();

        app.UseStaticFiles(new StaticFileOptions
        {
            // bcmap files needed for PDF reader localizations (https://github.com/Kareadita/Kavita/issues/2970)
            ContentTypeProvider = new FileExtensionContentTypeProvider
            {
                Mappings =
                {
                    [".bcmap"] = "application/octet-stream"
                }
            },
            HttpsCompression = HttpsCompressionMode.Compress,
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers[HeaderNames.CacheControl] = "public,max-age=" + TimeSpan.FromHours(24);
                ctx.Context.Response.Headers["X-Robots-Tag"] = "noindex,nofollow";
            }
        });

        app.UseSerilogRequestLogging(opts
            =>
        {
            opts.EnrichDiagnosticContext = LogEnricher.EnrichFromRequest;
            opts.IncludeQueryInRequestPath = true;
        });

        app.Use(async (context, next) =>
        {
            context.Response.Headers[HeaderNames.Vary] =
                new[] { "Accept-Encoding" };


            if (!Configuration.AllowIFraming)
            {
                // Don't let the site be iframed outside the same origin (clickjacking)
                context.Response.Headers.XFrameOptions = "SAMEORIGIN";

                // Setup CSP to ensure we load assets only from these origins
                context.Response.Headers.Add("Content-Security-Policy", "frame-ancestors 'none';");
            }
            else
            {
                logger.LogCritical("appsetting.json has allow iframing on! This may allow for clickjacking on the server. User beware");
            }

            await next();
        });

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapHub<MessageHub>("hubs/messages");
            endpoints.MapHub<LogHub>("hubs/logs");
            if (env.IsDevelopment())
            {
                endpoints.MapHangfireDashboard();
            }
            endpoints.MapFallbackToController("Index", "Fallback");
        });

        applicationLifetime.ApplicationStopping.Register(OnShutdown);
        applicationLifetime.ApplicationStarted.Register(() =>
        {
            try
            {
                logger.LogInformation("Kavita - v{Version}", BuildInfo.Version);
            }
            catch (Exception)
            {
                /* Swallow Exception */
                Console.WriteLine($"Kavita - v{BuildInfo.Version}");
            }
        });

        logger.LogInformation("Starting with base url as {BaseUrl}", basePath);
    }

    private static void UpdateBaseUrlInIndex(string baseUrl)
    {
        try
        {
            var htmlDoc = new HtmlDocument();
            var indexHtmlPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html");
            htmlDoc.Load(indexHtmlPath);

            var baseNode = htmlDoc.DocumentNode.SelectSingleNode("/html/head/base");
            baseNode.SetAttributeValue("href", baseUrl);
            htmlDoc.Save(indexHtmlPath);
        }
        catch (Exception ex)
        {
            if (ex is UnauthorizedAccessException && baseUrl.Equals(Configuration.DefaultBaseUrl) && OsInfo.IsDocker)
            {
                // Swallow the exception as the install is non-root and Docker
                return;
            }
            Log.Error(ex, "There was an error setting base url");
        }
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
