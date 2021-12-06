using System;
using System.IO;
using System.IO.Abstractions;
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
using NetVips;

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

            // var migrateLogger = LoggerFactory.Create(builder =>
            // {
            //     builder
            //         //.AddConfiguration(Configuration.GetSection("Logging"))
            //         .AddFilter("Microsoft", LogLevel.Warning)
            //         .AddFilter("System", LogLevel.Warning)
            //         .AddFilter("SampleApp.Program", LogLevel.Debug)
            //         .AddConsole()
            //         .AddEventLog();
            // });
            // var mLogger = migrateLogger.CreateLogger<DirectoryService>();
            // TODO: Figure out a solution for this migration and logger.
            var directoryService = new DirectoryService(null, new FileSystem());
            MigrateConfigFiles.Migrate(isDocker, directoryService);

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
                var context = services.GetRequiredService<DataContext>();
                var roleManager = services.GetRequiredService<RoleManager<AppRole>>();

                if (isDocker && new FileInfo("data/appsettings.json").Exists)
                {
                    var logger = services.GetRequiredService<ILogger<Startup>>();
                    logger.LogCritical("WARNING! Mount point is incorrect, nothing here will persist. Please change your container mount from /kavita/data to /kavita/config");
                    return;
                }

                // This doesn't work either
                //var directoryService = services.GetRequiredService<DirectoryService>();




                var requiresCoverImageMigration = !Directory.Exists(directoryService.CoverImageDirectory);
                try
                {
                    // If this is a new install, tables wont exist yet
                    if (requiresCoverImageMigration)
                    {
                        MigrateCoverImages.ExtractToImages(context, directoryService, services.GetRequiredService<ImageService>());
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
                    await MigrateCoverImages.UpdateDatabaseWithImages(context, directoryService);
                }

                await Seed.SeedRoles(roleManager);
                await Seed.SeedSettings(context, directoryService);
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




    }
}
