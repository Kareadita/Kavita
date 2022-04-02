﻿using System.IO.Abstractions;
using API.Data;
using API.Helpers;
using API.Services;
using API.Services.Tasks;
using API.SignalR;
using API.SignalR.Presence;
using Kavita.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API.Extensions
{
    public static class ApplicationServiceExtensions
    {
        public static void AddApplicationServices(this IServiceCollection services, IConfiguration config, IWebHostEnvironment env)
        {
            services.AddAutoMapper(typeof(AutoMapperProfiles).Assembly);
            services.AddScoped<IStatsService, StatsService>();
            services.AddScoped<ITaskScheduler, TaskScheduler>();
            services.AddScoped<IDirectoryService, DirectoryService>();
            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<ICacheService, CacheService>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IScannerService, ScannerService>();
            services.AddScoped<IArchiveService, ArchiveService>();
            services.AddScoped<IMetadataService, MetadataService>();
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
            services.AddScoped<ISiteThemeService, SiteThemeService>();
            services.AddScoped<ISeriesService, SeriesService>();


            services.AddScoped<IFileSystem, FileSystem>();
            services.AddScoped<IFileService, FileService>();
            services.AddScoped<ICacheHelper, CacheHelper>();


            services.AddScoped<IFileSystem, FileSystem>();
            services.AddScoped<IFileService, FileService>();
            services.AddScoped<ICacheHelper, CacheHelper>();

            services.AddScoped<IPresenceTracker, PresenceTracker>();
            services.AddScoped<IEventHub, EventHub>();

            services.AddSqLite(config, env);
            services.AddLogging(config);
            services.AddSignalR(opt => opt.EnableDetailedErrors = true);
        }

        private static void AddSqLite(this IServiceCollection services, IConfiguration config,
            IWebHostEnvironment env)
        {
            services.AddDbContext<DataContext>(options =>
            {
                options.UseSqlite(config.GetConnectionString("DefaultConnection"));
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
