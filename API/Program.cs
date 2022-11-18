using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using API.Data;
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

        var directoryService = new DirectoryService(null, new FileSystem());

        // Before anything, check if JWT has been generated properly or if user still has default
        if (!Configuration.CheckIfJwtTokenSet() &&
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != Environments.Development)
        {
            Console.WriteLine("Generating JWT TokenKey for encrypting user sessions...");
            var rBytes = new byte[128];
            RandomNumberGenerator.Create().GetBytes(rBytes);
            Configuration.JwtToken = Convert.ToBase64String(rBytes).Replace("/", string.Empty);
        }

        try
        {
            var host = CreateHostBuilder(args).Build();

            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider;

            try
            {
                var logger = services.GetRequiredService<ILogger<Program>>();
                var context = services.GetRequiredService<DataContext>();
                var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
                if (pendingMigrations.Any())
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

                // This must run before the migration
                try
                {
                    await MigrateSeriesRelationsExport.Migrate(context, logger);
                }
                catch (Exception)
                {
                    // If fresh install, could fail and we should just carry on as it's not applicable
                }

                await context.Database.MigrateAsync();

                await Seed.SeedRoles(services.GetRequiredService<RoleManager<AppRole>>());
                await Seed.SeedSettings(context, directoryService);
                await Seed.SeedThemes(context);
                await Seed.SeedUserApiKeys(context);
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
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();
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
        string currentVersion = null;
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
                    opts.ListenAnyIP(HttpPort, options => { options.Protocols = HttpProtocols.Http1AndHttp2; });
                });

                webBuilder.UseStartup<Startup>();
            });




}
