using System;
using System.Threading.Tasks;
using API.Data;
using API.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
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

        private static readonly int HttpPort = 5000; // TODO: Get from DB

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
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel((builderContext, opts) =>
                    {
                        opts.ListenAnyIP(HttpPort);
                    });
                    webBuilder.UseStartup<Startup>();
                });
        
        private static string BuildUrl(string scheme, string bindAddress, int port)
        {
            return $"{scheme}://{bindAddress}:{port}";
        }
        
        private static void ConfigureKestrelForHttps(KestrelServerOptions options)
        {
            options.ListenAnyIP(HttpPort);
            // options.ListenAnyIP(HttpsPort, listenOptions =>
            // {
            //     listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
            //     //listenOptions.UseHttps(pfxFilePath, pfxPassword);
            // });
        }
    }
}
