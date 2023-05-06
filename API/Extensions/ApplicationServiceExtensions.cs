using System.IO.Abstractions;
using API.Data;
using API.Helpers;
using API.Services;
using API.Services.Tasks;
using API.Services.Tasks.Metadata;
using API.Services.Tasks.Scanner;
using API.SignalR;
using API.SignalR.Presence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace API.Extensions;

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
        services.AddScoped<IProcessSeries, ProcessSeries>();
        services.AddScoped<IReadingListService, ReadingListService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<IStatisticService, StatisticService>();

        services.AddScoped<IScannerService, ScannerService>();
        services.AddScoped<IMetadataService, MetadataService>();
        services.AddScoped<IWordCountAnalyzerService, WordCountAnalyzerService>();
        services.AddScoped<ILibraryWatcher, LibraryWatcher>();
        services.AddScoped<ITachiyomiService, TachiyomiService>();
        services.AddScoped<ICollectionTagService, CollectionTagService>();

        services.AddScoped<IPresenceTracker, PresenceTracker>();
        services.AddScoped<IEventHub, EventHub>();

        services.AddSqLite(env);
        services.AddSignalR(opt => opt.EnableDetailedErrors = true);
    }

    private static void AddSqLite(this IServiceCollection services, IHostEnvironment env)
    {
        services.AddDbContext<DataContext>(options =>
        {
            options.UseSqlite("Data source=config/kavita.db");
            options.EnableDetailedErrors();

            options.EnableSensitiveDataLogging();
        });
    }
}
