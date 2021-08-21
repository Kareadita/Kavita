using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using API.Extensions;
using API.Middleware;
using API.Services;
using API.Services.HostedServices;
using API.SignalR;
using Hangfire;
using Hangfire.MemoryStorage;
using Kavita.Common.EnvironmentInfo;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

namespace API
{
    public class Startup
    {
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public Startup(IConfiguration config, IWebHostEnvironment env)
        {
            _config = config;
            _env = env;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddApplicationServices(_config, _env);
            services.AddControllers();
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            });
            services.AddCors();
            services.AddIdentityServices(_config);
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Kavita API", Version = "v1" });
                var filePath = Path.Combine(AppContext.BaseDirectory, "API.xml");
                c.IncludeXmlComments(filePath);
            });
            services.AddResponseCompression(options =>
            {
                options.Providers.Add<BrotliCompressionProvider>();
                options.Providers.Add<GzipCompressionProvider>();
                options.MimeTypes =
                    ResponseCompressionDefaults.MimeTypes.Concat(
                        new[] { "image/jpeg", "image/jpg" });
                options.EnableForHttps = true;
            });
            services.Configure<BrotliCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.Fastest;
            });

            services.AddResponseCaching();

            services.AddStatsClient(_config);

            services.AddHangfire(configuration => configuration
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseMemoryStorage());

            // Add the processing server as IHostedService
            services.AddHangfireServer();

            // Add IHostedService for startup tasks
            // Any services that should be bootstrapped go here
            services.AddHostedService<StartupTasksHostedService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IBackgroundJobClient backgroundJobs, IWebHostEnvironment env,
            IHostApplicationLifetime applicationLifetime)
        {
            app.UseMiddleware<ExceptionMiddleware>();

            if (env.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1"));
                app.UseHangfireDashboard();
            }

            app.UseResponseCompression();

            app.UseForwardedHeaders();

            app.UseRouting();

            // Ordering is important. Cors, authentication, authorization
            if (env.IsDevelopment())
            {
                app.UseCors(policy => policy
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials() // For SignalR token query param
                    .WithOrigins("http://localhost:4200")
                    .WithExposedHeaders("Content-Disposition", "Pagination"));
            }

            app.UseResponseCaching();

            app.UseAuthentication();

            app.UseAuthorization();

            app.UseDefaultFiles();

            app.UseStaticFiles(new StaticFileOptions
            {
                ContentTypeProvider = new FileExtensionContentTypeProvider()
            });

            app.Use(async (context, next) =>
            {
                context.Response.GetTypedHeaders().CacheControl =
                    new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
                    {
                        Public = false,
                        MaxAge = TimeSpan.FromSeconds(10)
                    };
                context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.Vary] =
                    new[] { "Accept-Encoding" };

                await next();
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<MessageHub>("hubs/messages");
                endpoints.MapHub<PresenceHub>("hubs/presence");
                endpoints.MapHangfireDashboard();
                endpoints.MapFallbackToController("Index", "Fallback");
            });

            applicationLifetime.ApplicationStopping.Register(OnShutdown);
            applicationLifetime.ApplicationStarted.Register(() =>
            {
                Console.WriteLine($"Kavita - v{BuildInfo.Version}");
            });
        }

        private static void OnShutdown()
        {
            Console.WriteLine("Server is shutting down. Please allow a few seconds to stop any background jobs...");
            TaskScheduler.Client.Dispose();
            System.Threading.Thread.Sleep(1000);
            Console.WriteLine("You may now close the application window.");
        }


    }
}
