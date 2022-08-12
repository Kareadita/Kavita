using System;
using System.IO.Abstractions;
using API.Data;
using API.Helpers;
using API.Services;
using API.Services.Tasks;
using API.Services.Tasks.Metadata;
using API.SignalR;
using API.SignalR.Presence;
using Kavita.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;

namespace API.Extensions
{
    public static class ApplicationServiceExtensions
    {
        public static void AddApplicationServices(this IServiceCollection services, IConfiguration config, IWebHostEnvironment env)
        {
            services.AddAutoMapper(typeof(AutoMapperProfiles).Assembly);

            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IDirectoryService, DirectoryService>();
            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<IFileSystem, FileSystem>();
            services.AddScoped<IFileService, FileService>();
            services.AddScoped<ICacheHelper, CacheHelper>();

            services.AddScoped<IStatsService, StatsService>();
            services.AddScoped<ITaskScheduler, TaskScheduler>();
            services.AddScoped<ICacheService, CacheService>();
            services.AddScoped<IArchiveService, ArchiveService>();
            services.AddScoped<IBackupService, BackupService>();
            services.AddScoped<ICleanupService, CleanupService>();
            services.AddScoped<IBookService, BookService>();
            services.AddScoped<IImageService, ImageService>();
            services.AddScoped<IVersionUpdaterService, VersionUpdaterService>();
            services.AddScoped<IDownloadService, DownloadService>();
            services.AddScoped<IReaderService, ReaderService>();
            services.AddScoped<IReadingItemService, ReadingItemService>();
            services.AddScoped<IAccountService, AccountService>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IBookmarkService, BookmarkService>();
            services.AddScoped<IThemeService, ThemeService>();
            services.AddScoped<ISeriesService, SeriesService>();

            services.AddScoped<IScannerService, ScannerService>();
            services.AddScoped<IMetadataService, MetadataService>();
            services.AddScoped<IWordCountAnalyzerService, WordCountAnalyzerService>();



            services.AddScoped<IPresenceTracker, PresenceTracker>();
            services.AddScoped<IEventHub, EventHub>();

            services.AddSqLite(config, env);
            services.AddLogging(config);
            services.AddSignalR(opt => opt.EnableDetailedErrors = true);
        }

        private static void AddSqLite(this IServiceCollection services, IConfiguration config,
            IHostEnvironment env)
        {
            services.AddDbContext<DataContext>(options =>
            {
                var conn = new SqliteConnection(config.GetConnectionString("DefaultConnection"));
                conn.CreateCollation("NOCASE", (x, y) => string.Compare(x, y, StringComparison.CurrentCultureIgnoreCase));

                options.UseSqlite(conn);
                //options.UseSqlite(config.GetConnectionString("DefaultConnection"));
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging(env.IsDevelopment() || Configuration.LogLevel.Equals("Debug"));
            });
        }

        private static void AddLogging(this IServiceCollection services, IConfiguration config)
        {
          services.AddLogging(loggingBuilder =>
          {
            var loggingSection = config.GetSection("Logging");
            loggingBuilder.AddFile(loggingSection);
          });
        }
    }
}
