using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using API.Data;
using API.Entities;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sentry;

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
            // Apply all migrations on startup
            await context.Database.MigrateAsync();
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
            .ConfigureWebHostDefaults(webBuilder =>
            {
               webBuilder.UseKestrel((opts) =>
               {
                  opts.ListenAnyIP(HttpPort, options => { options.Protocols = HttpProtocols.Http1AndHttp2; });
               });

               var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
               if (environment != Environments.Development)
               {
                  webBuilder.UseSentry(options =>
                  {
                     options.Dsn = "https://40f4e7b49c094172a6f99d61efb2740f@o641015.ingest.sentry.io/5757423";
                     options.MaxBreadcrumbs = 200;
                     options.AttachStacktrace = true;
                     options.Debug = false;
                     options.SendDefaultPii = false;
                     options.DiagnosticLevel = SentryLevel.Debug;
                     options.ShutdownTimeout = TimeSpan.FromSeconds(5);
                     options.Release = BuildInfo.Version.ToString();
                     options.AddExceptionFilterForType<OutOfMemoryException>();
                     options.AddExceptionFilterForType<NetVips.VipsException>();
                     options.AddExceptionFilterForType<InvalidDataException>();
                     options.AddExceptionFilterForType<KavitaException>();

                     options.BeforeSend = sentryEvent =>
                     {
                        if (sentryEvent.Exception != null
                            && sentryEvent.Exception.Message.StartsWith("[GetCoverImage]")
                            && sentryEvent.Exception.Message.StartsWith("[BookService]")
                            && sentryEvent.Exception.Message.StartsWith("[ExtractArchive]")
                            && sentryEvent.Exception.Message.StartsWith("[GetSummaryInfo]")
                            && sentryEvent.Exception.Message.StartsWith("[GetSummaryInfo]")
                            && sentryEvent.Exception.Message.StartsWith("[GetNumberOfPagesFromArchive]")
                            && sentryEvent.Exception.Message.Contains("EPUB parsing error")
                            && sentryEvent.Exception.Message.Contains("Unsupported EPUB version")
                            && sentryEvent.Exception.Message.Contains("Incorrect EPUB")
                            && sentryEvent.Exception.Message.Contains("Access is Denied"))
                        {
                           return null; // Don't send this event to Sentry
                        }

                        sentryEvent.ServerName = null; // Never send Server Name to Sentry
                        return sentryEvent;
                     };

                     options.ConfigureScope(scope =>
                     {
                        scope.User = new User()
                        {
                           Id = HashUtil.AnonymousToken()
                        };
                        scope.Contexts.App.Name = BuildInfo.AppName;
                        scope.Contexts.App.Version = BuildInfo.Version.ToString();
                        scope.Contexts.App.StartTime = DateTime.UtcNow;
                        scope.Contexts.App.Hash = HashUtil.AnonymousToken();
                        scope.Contexts.App.Build = BuildInfo.Release;
                        scope.SetTag("culture", Thread.CurrentThread.CurrentCulture.Name);
                        scope.SetTag("branch", BuildInfo.Branch);
                     });
                  });
               }

               webBuilder.UseStartup<Startup>();
            });
   }
}
