using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using API.Data;
using API.Entities;
using API.Entities.Enums;
using API.Services;
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

namespace API
{
    public class Program
    {
        private static readonly int HttpPort = Configuration.Port;

        protected Program()
        {
        }

        public static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            var isDocker = new OsInfo(Array.Empty<IOsVersionAdapter>()).IsDocker;


            var directoryService = new DirectoryService(null, new FileSystem());
            //MigrateConfigFiles.Migrate(isDocker, directoryService);

            // Before anything, check if JWT has been generated properly or if user still has default
            if (!Configuration.CheckIfJwtTokenSet() &&
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != Environments.Development)
            {
                Console.WriteLine("Generating JWT TokenKey for encrypting user sessions...");
                var rBytes = new byte[128];
                RandomNumberGenerator.Create().GetBytes(rBytes);
                Configuration.JwtToken = Convert.ToBase64String(rBytes).Replace("/", string.Empty);
            }

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

                await context.Database.MigrateAsync();
                var roleManager = services.GetRequiredService<RoleManager<AppRole>>();

                await Seed.SeedRoles(roleManager);
                await Seed.SeedSettings(context, directoryService);
                await Seed.SeedUserApiKeys(context);


                if (isDocker && new FileInfo("data/appsettings.json").Exists)
                {
                    logger.LogCritical("WARNING! Mount point is incorrect, nothing here will persist. Please change your container mount from /kavita/data to /kavita/config");
                    return;
                }
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

            await host.RunAsync();
        }

        private static async Task<string> GetMigrationDirectory(DataContext context, IDirectoryService directoryService)
        {
            string currentVersion = null;
            try
            {
                currentVersion =
                    (await context.ServerSetting.SingleOrDefaultAsync(s =>
                        s.Key == ServerSettingKey.InstallVersion))?.Value;
            }
            catch
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
}
