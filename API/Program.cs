using System;
using System.Threading.Tasks;
using API.Data;
using API.Entities;
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
            
            await host.RunAsync();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                // .ConfigureLogging(logging =>
                // {
                //     logging.ClearProviders();
                //     logging.AddConsole();
                // })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
