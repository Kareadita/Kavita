using System;
using System.Globalization;
using System.IO.Abstractions;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using API.Data;
using API.Data.ManualMigrations;
using API.Entities;
using API.Entities.Enums;
using API.Logging;
using API.Services;
using API.SignalR;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.AspNetCore.SignalR.Extensions;

namespace API;
#nullable enable

public class Program
{
    private static readonly int HttpPort = Configuration.Port;

    protected Program()
    {
    }

    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel
            .Information()
            .CreateBootstrapLogger();

        var directoryService = new DirectoryService(null!, new FileSystem());

        // Before anything, check if JWT has been generated properly or if user still has default
        if (!Configuration.CheckIfJwtTokenSet() &&
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != Environments.Development)
        {
            Log.Logger.Information("Generating JWT TokenKey for encrypting user sessions...");
            var rBytes = new byte[256];
            RandomNumberGenerator.Create().GetBytes(rBytes);
            Configuration.JwtToken = Convert.ToBase64String(rBytes).Replace("/", string.Empty);
        }

        Configuration.KavitaPlusApiUrl = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == Environments.Development
            ?  "http://localhost:5020" : "https://plus.kavitareader.com";

        try
        {
            var host = CreateHostBuilder(args).Build();

            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider;
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();

            try
            {
                var logger = services.GetRequiredService<ILogger<Program>>();
                var context = services.GetRequiredService<DataContext>();
                var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
                var isDbCreated = await context.Database.CanConnectAsync();
                if (isDbCreated && pendingMigrations.Any())
                {
                    logger.LogInformation("Performing backup as migrations are needed. Backup will be kavita.db in temp folder");
                    var migrationDirectory = await GetMigrationDirectory(context, directoryService);
                    directoryService.ExistOrCreate(migrationDirectory);

                    if (!directoryService.FileSystem.File.Exists(
                            directoryService.FileSystem.Path.Join(migrationDirectory, "kavita.db")))
                    {
                        directoryService.CopyFileToDirectory(directoryService.FileSystem.Path.Join(directoryService.ConfigDirectory, "kavita.db"), migrationDirectory);
                        logger.LogInformation("Database backed up to {MigrationDirectory}", migrationDirectory);
                    }
                }

                // Apply Before manual migrations that need to run before actual migrations
                if (isDbCreated)
                {
                    Task.Run(async () =>
                        {
                            // Apply all migrations on startup
                            logger.LogInformation("Running Manual Migrations");

                            try
                            {
                                // v0.7.14
                                await MigrateWantToReadExport.Migrate(context, directoryService, logger);

                                // v0.8.2
                                await ManualMigrateSwitchToWal.Migrate(context, logger);

                                // v0.8.4
                                await ManualMigrateEncodeSettings.Migrate(context, logger);
                            }
                            catch (Exception ex)
                            {
                                /* Swallow */
                            }

                            await unitOfWork.CommitAsync();
                            logger.LogInformation("Running Manual Migrations - complete");
                        }).GetAwaiter()
                        .GetResult();
                }



                await context.Database.MigrateAsync();


                await Seed.SeedRoles(services.GetRequiredService<RoleManager<AppRole>>());
                await Seed.SeedSettings(context, directoryService);
                await Seed.SeedThemes(context);
                await Seed.SeedDefaultStreams(unitOfWork);
                await Seed.SeedDefaultSideNavStreams(unitOfWork);
                await Seed.SeedUserApiKeys(context);
                await Seed.SeedMetadataSettings(context);
            }
            catch (Exception ex)
            {
                var logger = services.GetRequiredService<ILogger<Program>>();
                var context = services.GetRequiredService<DataContext>();
                var migrationDirectory = await GetMigrationDirectory(context, directoryService);

                logger.LogCritical(ex, "A migration failed during startup. Restoring backup from {MigrationDirectory} and exiting", migrationDirectory);
                directoryService.CopyFileToDirectory(directoryService.FileSystem.Path.Join(migrationDirectory, "kavita.db"), directoryService.ConfigDirectory);

                return;
            }

            // Update the logger with the log level
            var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
            LogLevelOptions.SwitchLogLevel(settings.LoggingLevel);

            await host.RunAsync();
        } catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
        } finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    private static async Task<string> GetMigrationDirectory(DataContext context, IDirectoryService directoryService)
    {
        string? currentVersion = null;
        try
        {
            if (!await context.ServerSetting.AnyAsync()) return "vUnknown";
            currentVersion =
                (await context.ServerSetting.SingleOrDefaultAsync(s =>
                    s.Key == ServerSettingKey.InstallVersion))?.Value;
        }
        catch (Exception)
        {
            // ignored
        }

        if (string.IsNullOrEmpty(currentVersion))
        {
            currentVersion = "vUnknown";
        }

        var migrationDirectory = directoryService.FileSystem.Path.Join(directoryService.TempDirectory,
            "migration", currentVersion);
        return migrationDirectory;
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog((_, services, configuration) =>
            {
                LogLevelOptions.CreateConfig(configuration)
                    .WriteTo.SignalRSink<LogHub, ILogHub>(
                        LogEventLevel.Information,
                        services);
            })
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.Sources.Clear();

                var env = hostingContext.HostingEnvironment;

                config.AddJsonFile("config/appsettings.json", optional: true, reloadOnChange: false)
                    .AddJsonFile($"config/appsettings.{env.EnvironmentName}.json",
                        optional: true, reloadOnChange: false);
            })
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseKestrel((opts) =>
                {
                    var ipAddresses = Configuration.IpAddresses;
                    if (OsInfo.IsDocker || string.IsNullOrEmpty(ipAddresses) || ipAddresses.Equals(Configuration.DefaultIpAddresses))
                    {
                        opts.ListenAnyIP(HttpPort, options => { options.Protocols = HttpProtocols.Http1AndHttp2; });
                    }
                    else
                    {
                        foreach (var ipAddress in ipAddresses.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                        {
                            try
                            {
                                var address = System.Net.IPAddress.Parse(ipAddress.Trim());
                                opts.Listen(address, HttpPort, options => { options.Protocols = HttpProtocols.Http1AndHttp2; });
                            }
                            catch (Exception ex)
                            {
                                Log.Fatal(ex, "Could not parse ip address {IPAddress}", ipAddress);
                            }
                        }
                    }
                });

                webBuilder.UseStartup<Startup>();
            });
}
