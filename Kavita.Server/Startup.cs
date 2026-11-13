using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using HtmlAgilityPack;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Plus;
using Kavita.Common;
using Kavita.Common.Constants;
using Kavita.Common.EnvironmentInfo;
using Kavita.Database;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.Internal;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Extensions;
using Kavita.Server.Extensions;
using Kavita.Server.Logging;
using Kavita.Server.ManualMigrations;
using Kavita.Server.ManualMigrations.v0._7._11;
using Kavita.Server.ManualMigrations.v0._7._14;
using Kavita.Server.ManualMigrations.v0._7._9;
using Kavita.Server.ManualMigrations.v0._8._0;
using Kavita.Server.ManualMigrations.v0._8._1;
using Kavita.Server.ManualMigrations.v0._8._2;
using Kavita.Server.ManualMigrations.v0._8._4;
using Kavita.Server.ManualMigrations.v0._8._5;
using Kavita.Server.ManualMigrations.v0._8._6;
using Kavita.Server.ManualMigrations.v0._8._7;
using Kavita.Server.ManualMigrations.v0._8._8;
using Kavita.Server.ManualMigrations.v0._8._9;
using Kavita.Server.ManualMigrations.v0._9._0;
using Kavita.Server.ManualMigrations.v0._9._1;
using Kavita.Server.ManualMigrations.v0._9._1.x;
using Kavita.Server.ManualMigrations.v0._9._2;
using Kavita.Server.Middleware;
using Kavita.Server.Swagger;
using Kavita.Services.SignalR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi;
using Serilog;
using TaskScheduler = Kavita.Services.TaskScheduler;

namespace Kavita.Server;

public class Startup
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public Startup(IConfiguration config, IWebHostEnvironment env)
    {
        _config = config;
        _env = env;

        // Disable Hangfire Automatic Retry
        GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute { Attempts = 0 });
        GlobalJobFilters.Filters.Add(new JobCorrelationFilter());
    }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<AppSettingsDto>(_config);
        services.AddMappings();
        services.AddApplicationServices(_config, _env);

        services.AddControllers(options =>
        {
            options.CacheProfiles.Add(ResponseCacheProfiles.Minute,
                new CacheProfile()
                {
                    Duration = 60 * 1,
                    Location = ResponseCacheLocation.Client,
                });
            options.CacheProfiles.Add(ResponseCacheProfiles.FiveMinute,
                new CacheProfile()
                {
                    Duration = 60 * 5,
                    Location = ResponseCacheLocation.Client,
                });
            options.CacheProfiles.Add(ResponseCacheProfiles.TenMinute,
                new CacheProfile()
                {
                    Duration = 60 * 10,
                    Location = ResponseCacheLocation.Client,
                    NoStore = false
                });
            options.CacheProfiles.Add(ResponseCacheProfiles.Hour,
                new CacheProfile()
                {
                    Duration = 60 * 60,
                    Location = ResponseCacheLocation.Client,
                    NoStore = false
                });
            options.CacheProfiles.Add(ResponseCacheProfiles.Statistics,
                new CacheProfile()
                {
                    Duration = _env.IsDevelopment() ? 0 : 60 * 60 * 6,
                    Location = ResponseCacheLocation.Client,
                });
            options.CacheProfiles.Add(ResponseCacheProfiles.Month,
                new CacheProfile()
                {
                    Duration = (int) TimeSpan.FromDays(30).TotalSeconds,
                    Location = ResponseCacheLocation.Client,
                    NoStore = false
                });
            options.CacheProfiles.Add(ResponseCacheProfiles.LicenseCache,
                new CacheProfile()
                {
                    Duration = (int) TimeSpan.FromHours(4).TotalSeconds,
                    Location = ResponseCacheLocation.Client,
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
        services.AddIdentityServices(_config, _env);


        services.AddSwaggerGen(c =>
        {
            c.SchemaFilter<EnumSchemaFilter>();

            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Version = BuildInfo.Version.ToString(),
                Title = $"Kavita",
                Description = $"Kavita provides a set of APIs authenticated via an Auth Key passed in the `x-api-key` header. Generate an Auth Key under User Settings → Manage Auth Keys, paste it into the Authorize panel, and all Try It requests will include it. Assume all fields of a payload are required unless marked optional. Built against v{BuildInfo.Version}",
                License = new OpenApiLicense
                {
                    Name = "GPL-3.0",
                    Url = new Uri("https://github.com/Kareadita/Kavita/blob/develop/LICENSE")
                },
            });

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var filePath = Path.Combine(AppContext.BaseDirectory, xmlFile);

            c.IncludeXmlComments(filePath, true);

            c.AddSecurityDefinition("AuthKey", new OpenApiSecurityScheme
            {
                Description = "Auth Key authentication. Enter your Auth key from your user settings",
                Name = Headers.ApiKey,
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "ApiKeyScheme"
            });

            c.AddSecurityRequirement((document) => new OpenApiSecurityRequirement()
            {
                [new OpenApiSecuritySchemeReference("AuthKey", document)] = []
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

        AddCompressionAndCaching(services);

        services.AddRateLimiter(options =>
        {
            options.AddPolicy("Authentication", httpContext =>
                new AuthenticationRateLimiterPolicy().GetPartition(httpContext));
        });

        services.AddHangfire((provider, configuration) => configuration
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseInMemoryStorage()
            .UseSerilogLogProvider()
            .UseFilter(ActivatorUtilities.CreateInstance<ScanEndRetimeFilter>(provider)));
            //.UseSQLiteStorage("config/Hangfire.db"));
            //// UseSQLiteStorage - SQLite has some issues around resuming jobs when aborted (and locking can cause high utilization)
            /// (NOTE: There is code to clear jobs on startup a redditor gave me)

        // Add the processing server as IHostedService
        services.AddHangfireServer(options =>
        {
            options.Queues = [TaskSchedulerConstants.ScanQueue, TaskSchedulerConstants.DefaultQueue];
        });
    }

    private static void AddCompressionAndCaching(IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes =
                ResponseCompressionDefaults.MimeTypes.Concat(
                    ["image/jpeg", "image/jpg", "image/png", "image/avif", "image/gif", "image/webp", "image/tiff"]);
            options.EnableForHttps = true;
        });
        services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Fastest;
        });

        services.AddResponseCaching();
        services.AddHybridCache(options =>
        {
            options.MaximumPayloadBytes = 1024 * 1024; // 1MB max per entry
            options.MaximumKeyLength = 512;
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(10),
                LocalCacheExpiration = TimeSpan.FromMinutes(10)
            };
        });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env,
        IHostApplicationLifetime applicationLifetime, IServiceProvider serviceProvider,
        IDirectoryService directoryService, IUnitOfWork unitOfWork, IVersionUpdaterService versionService)
    {

        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();


        Task.Run(async () =>
            {
                var migrationService = serviceProvider.GetRequiredService<IMigrationService>();
                await migrationService.RunPostMigrations();
            }).GetAwaiter() .GetResult();

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

        // Ensure static files is before our custom middleware stack
        app.UseStaticFiles(new StaticFileOptions
        {
            // bcmap files needed for PDF reader localizations (https://github.com/Kareadita/Kavita/issues/2970)
            // ftl files are needed for PDF zoom options (https://github.com/Kareadita/Kavita/issues/3995)
            ContentTypeProvider = new FileExtensionContentTypeProvider
            {
                Mappings =
                {
                    [".bcmap"] = "application/octet-stream",
                    [".ftl"] = "text/plain"
                }
            },
            HttpsCompression = HttpsCompressionMode.Compress,
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers[HeaderNames.CacheControl] = ctx.File.Name == "index.html"
                    ? "no-cache"
                    : "public,max-age=" + (int) TimeSpan.FromHours(24).TotalSeconds;
                ctx.Context.Response.Headers[Headers.RobotsTag] = "noindex,nofollow";
            }
        });

        app.UseAuthentication();
        app.UseAuthorization();

        // Must be first after Auth, will set authentication data for the rest of the Controllers/Middleware
        app.UseMiddleware<UserContextMiddleware>();
        app.UseMiddleware<ClientInfoMiddleware>();
        app.UseMiddleware<DeviceTrackingMiddleware>(); // This must be after ClientInfo and Authorization
        app.UseMiddleware<UpdateUserAsActiveMiddleware>(); // This must be LAST

        app.UseDefaultFiles();



        app.UseSerilogRequestLogging(opts
            =>
        {
            opts.EnrichDiagnosticContext = LogEnricher.EnrichFromRequest;
            opts.IncludeQueryInRequestPath = true;
        });

        if (Configuration.AllowIFraming)
        {
            logger.LogCritical("appsetting.json has allow iframing on! This may allow for clickjacking on the server. User beware");
        }

        app.Use(async (context, next) =>
        {
            context.Response.Headers[HeaderNames.Vary] = new[] { "Accept-Encoding" };

            if (!Configuration.AllowIFraming)
            {
                // Don't let the site be iframed outside the same origin (clickjacking)
                context.Response.Headers.XFrameOptions = "SAMEORIGIN";

                // Setup CSP to ensure we load assets only from these origins
                context.Response.Headers.ContentSecurityPolicy = "frame-ancestors 'none';";
            }

            await next();
        });

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapHub<MessageHub>("hubs/messages");
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
                logger.LogInformation("Kavita - v{Version}, boot {BootId}", BuildInfo.Version, BuildInfo.BootId);
            }
            catch (Exception)
            {
                Console.WriteLine($"Kavita - v{BuildInfo.Version}, boot {BuildInfo.BootId}");
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = app.ApplicationServices.CreateScope();
                    var licenseService = scope.ServiceProvider.GetRequiredService<ILicenseService>();
                    await licenseService.HasActiveLicense(true);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to warm license cache on startup");
                }
                finally
                {
                    // Always enqueue even if the license check failed (ScheduleKavitaPlusTasks handles invalid/missing license)
                    BackgroundJob.Enqueue<ITaskScheduler>(s => s.ScheduleKavitaPlusTasks(CancellationToken.None));
                }
            });
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
        Thread.Sleep(1000);
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
