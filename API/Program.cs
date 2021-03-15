using System;
using System.Threading.Tasks;
using API.Data;
using API.Entities;
using API.Interfaces;
using API.Interfaces.Services;
using API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API
{
    public class Program
    {
        private static readonly int HttpPort = 5000;
        protected Program()
        {
        }

        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider;

            try
            {
                var context = services.GetRequiredService<DataContext>();
                var roleManager = services.GetRequiredService<RoleManager<AppRole>>();
                // Apply all migrations on startup
                await context.Database.MigrateAsync();
                await Seed.SeedRoles(roleManager);
                await Seed.SeedSettings(context);
            }
            catch (Exception ex)
            {
                var logger = services.GetRequiredService < ILogger<Program>>();
                logger.LogError(ex, "An error occurred during migration");
            }
            
            // Load all tasks from DI (TODO: This is not working)
            var startupTasks = host.Services.GetServices<WarmupServicesStartupTask>();

            // Execute all the tasks
            foreach (var startupTask in startupTasks)
            {
                await startupTask.ExecuteAsync();
            }

            await host.RunAsync();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel((opts) =>
                    {
                        opts.ListenAnyIP(HttpPort);
                    });
                    webBuilder.UseStartup<Startup>();
                });

        // private static void StartNewInstance()
        // {
        //     //_logger.LogInformation("Starting new instance");
        //
        //     var module = options.RestartPath;
        //
        //     if (string.IsNullOrWhiteSpace(module))
        //     {
        //         module = Environment.GetCommandLineArgs()[0];
        //     }
        //
        //     // string commandLineArgsString;
        //     // if (options.RestartArgs != null)
        //     // {
        //     //     commandLineArgsString = options.RestartArgs ?? string.Empty;
        //     // }
        //     // else
        //     // {
        //     //     commandLineArgsString = string.Join(
        //     //         ' ',
        //     //         Environment.GetCommandLineArgs().Skip(1).Select(NormalizeCommandLineArgument));
        //     // }
        //
        //     //_logger.LogInformation("Executable: {0}", module);
        //     //_logger.LogInformation("Arguments: {0}", commandLineArgsString);
        //
        //     Process.Start(module, Array.Empty<string>);
        // }
    }
}
