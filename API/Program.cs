using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using API.Data;
using API.Entities;
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

            MigrateConfigFiles(isDocker);

            // Before anything, check if JWT has been generated properly or if user still has default
            if (!Configuration.CheckIfJwtTokenSet() &&
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != Environments.Development)
            {
                Console.WriteLine("Generating JWT TokenKey for encrypting user sessions...");
                var rBytes = new byte[128];
                using (var crypto = new RNGCryptoServiceProvider()) crypto.GetBytes(rBytes);
                Configuration.JwtToken = Convert.ToBase64String(rBytes).Replace("/", string.Empty);
            }

            var host = CreateHostBuilder(args).Build();

            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider;

            try
            {
                var context = services.GetRequiredService<DataContext>();
                var roleManager = services.GetRequiredService<RoleManager<AppRole>>();

                if (isDocker && new FileInfo("data/appsettings.json").Exists)
                {
                    var logger = services.GetRequiredService<ILogger<Startup>>();
                    logger.LogCritical("WARNING! Mount point is incorrect, nothing here will persist. Please change your container mount from /kavita/data to /kavita/config");
                    return;
                }


                var requiresCoverImageMigration = !Directory.Exists(DirectoryService.CoverImageDirectory);
                try
                {
                    // If this is a new install, tables wont exist yet
                    if (requiresCoverImageMigration)
                    {
                        MigrateCoverImages.ExtractToImages(context);
                    }
                }
                catch (Exception)
                {
                    requiresCoverImageMigration = false;
                }

                // Apply all migrations on startup
                await context.Database.MigrateAsync();

                if (requiresCoverImageMigration)
                {
                    await MigrateCoverImages.UpdateDatabaseWithImages(context);
                }

                await Seed.SeedRoles(roleManager);
                await Seed.SeedSettings(context);
                await Seed.SeedUserApiKeys(context);
            }
            catch (Exception ex)
            {
                var logger = services.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "An error occurred during migration");
            }

            await host.RunAsync();
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

        /// <summary>
        /// In v0.4.8 we moved all config files to config/ to match with how docker was setup. This will move all config files from current directory
        /// to config/
        /// </summary>
        private static void MigrateConfigFiles(bool isDocker)
        {
            Console.WriteLine("Checking if migration to config/ is needed");

            if (isDocker)
            {
                Console.WriteLine(
                    "Migrating files from pre-v0.4.8. All Kavita config files are now located in config/");

                Console.WriteLine("Updating appsettings.json to new paths");
                Configuration.DatabasePath = "config//kavita.db";
                Configuration.LogPath = "config//logs/kavita.log";
                Console.WriteLine("Updating appsettings.json to new paths...DONE");
                DirectoryService.ClearAndDeleteDirectory(Path.Join(Directory.GetCurrentDirectory(), "data"));

                Console.WriteLine("Migration complete. All config files are now in config/ directory");
                return;
            }

            if (!new FileInfo(Path.Join(Directory.GetCurrentDirectory(), "appsettings.json")).Exists)
            {
                Console.WriteLine("Migration to config/ not needed");
                return;
            }

            Console.WriteLine(
                "Migrating files from pre-v0.4.8. All Kavita config files are now located in config/");

            var configDirectory = Path.Join(Directory.GetCurrentDirectory(), "config");
            Console.WriteLine($"Creating {configDirectory}");
            DirectoryService.ExistOrCreate(configDirectory);
            var configFiles = new List<string>()
            {
                "appsettings.json",
                "appsettings.Development.json",
                "kavita.db",
            }.Select(file => new FileInfo(Path.Join(Directory.GetCurrentDirectory(), file)))
                .Where(f => f.Exists)
                .ToList();
            // First step is to move all the files
            Console.WriteLine("Moving files to config/");
            foreach (var fileInfo in configFiles)
            {
                try
                {
                    fileInfo.CopyTo(Path.Join(configDirectory, fileInfo.Name));
                }
                catch (Exception)
                {
                    /* Swallow exception when already exists */
                }
            }
            Console.WriteLine("Moving files to config...DONE");

            Console.WriteLine("Moving folders to config");
            var foldersToMove = new List<string>()
            {
                "covers",
                "stats",
                "logs",
                "backups",
                "cache",
                "temp"
            };
            foreach (var folderToMove in foldersToMove)
            {
                if (new DirectoryInfo(Path.Join(configDirectory, folderToMove)).Exists) continue;

                DirectoryService.CopyDirectoryToDirectory(Path.Join(Directory.GetCurrentDirectory(), folderToMove),
                    Path.Join(configDirectory, folderToMove));
            }
            Console.WriteLine("Moving folders to config...DONE");

            // Then we need to update the config file to point to the new DB file
            Console.WriteLine("Updating appsettings.json to new paths");
            Configuration.DatabasePath = "config//kavita.db";
            Configuration.LogPath = "config//logs/kavita.log";
            Console.WriteLine("Updating appsettings.json to new paths...DONE");

            // Finally delete everything in the source directory
            Console.WriteLine("Removing old files");
            DirectoryService.DeleteFiles(configFiles.Select(f => f.FullName));
            foreach (var folderToDelete in foldersToMove)
            {
                if (!new DirectoryInfo(Path.Join(Directory.GetCurrentDirectory(), folderToDelete)).Exists) continue;

                DirectoryService.ClearAndDeleteDirectory(Path.Join(Directory.GetCurrentDirectory(), folderToDelete));
            }

            // if (isDocker && new DirectoryInfo(Path.Join(Directory.GetCurrentDirectory(), "data")).Exists)
            // {
            //     DirectoryService.ClearAndDeleteDirectory(Path.Join(Directory.GetCurrentDirectory(), "data"));
            // }
            Console.WriteLine("Removing old files...DONE");

            Console.WriteLine("Migration complete. All config files are now in config/ directory");
        }


    }
}
