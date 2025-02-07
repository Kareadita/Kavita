using System.IO.Abstractions;
using API.Constants;
using API.Data;
using API.Helpers;
using API.Services;
using API.Services.Plus;
using API.Services.Tasks;
using API.Services.Tasks.Metadata;
using API.Services.Tasks.Scanner;
using API.SignalR;
using API.SignalR.Presence;
using Kavita.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace API.Extensions;


public static class ApplicationServiceExtensions
{
    public static void AddApplicationServices(this IServiceCollection services, IConfiguration config, IWebHostEnvironment env)
    {
        services.AddAutoMapper(typeof(AutoMapperProfiles).Assembly);

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<ICacheHelper, CacheHelper>();

        services.AddScoped<IStatsService, StatsService>();
        services.AddScoped<ITaskScheduler, TaskScheduler>();
        services.AddScoped<ICacheService, CacheService>();
        services.AddScoped<IArchiveService, ArchiveService>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<ICleanupService, CleanupService>();
        services.AddScoped<IBookService, BookService>();
        services.AddScoped<IVersionUpdaterService, VersionUpdaterService>();
        services.AddScoped<IDownloadService, DownloadService>();
        services.AddScoped<IReaderService, ReaderService>();
        services.AddScoped<IReadingItemService, ReadingItemService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IBookmarkService, BookmarkService>();
        services.AddScoped<IThemeService, ThemeService>();
        services.AddScoped<ISeriesService, SeriesService>();
        services.AddScoped<IReadingListService, ReadingListService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<IStatisticService, StatisticService>();
        services.AddScoped<IMediaErrorService, MediaErrorService>();
        services.AddScoped<IMediaConversionService, MediaConversionService>();
        services.AddScoped<IStreamService, StreamService>();

        services.AddScoped<IScannerService, ScannerService>();
        services.AddScoped<IProcessSeries, ProcessSeries>();
        services.AddScoped<IMetadataService, MetadataService>();
        services.AddScoped<IWordCountAnalyzerService, WordCountAnalyzerService>();
        services.AddScoped<ILibraryWatcher, LibraryWatcher>();
        services.AddScoped<ITachiyomiService, TachiyomiService>();
        services.AddScoped<ICollectionTagService, CollectionTagService>();

        services.AddScoped<IFileSystem, FileSystem>();
        services.AddScoped<IDirectoryService, DirectoryService>();
        services.AddScoped<IEventHub, EventHub>();
        services.AddScoped<IPresenceTracker, PresenceTracker>();
        services.AddScoped<IImageService, ImageService>();
        services.AddScoped<ICoverDbService, CoverDbService>();

        services.AddScoped<ILocalizationService, LocalizationService>();


        services.AddScoped<IScrobblingService, ScrobblingService>();
        services.AddScoped<ILicenseService, LicenseService>();
        services.AddScoped<IExternalMetadataService, ExternalMetadataService>();
        services.AddScoped<ISmartCollectionSyncService, SmartCollectionSyncService>();
        services.AddScoped<IWantToReadSyncService, WantToReadSyncService>();

        services.AddSqLite();
        services.AddSignalR(opt => opt.EnableDetailedErrors = true);

        services.AddEasyCaching(options =>
        {
            options.UseInMemory(EasyCacheProfiles.Favicon);
            options.UseInMemory(EasyCacheProfiles.Library);
            options.UseInMemory(EasyCacheProfiles.RevokedJwt);

            // KavitaPlus stuff
            options.UseInMemory(EasyCacheProfiles.KavitaPlusExternalSeries);
            options.UseInMemory(EasyCacheProfiles.License);
            options.UseInMemory(EasyCacheProfiles.LicenseInfo);
            options.UseInMemory(EasyCacheProfiles.KavitaPlusMatchSeries);
        });

        services.AddMemoryCache(options =>
        {
            options.SizeLimit = Configuration.CacheSize * 1024 * 1024; // 75 MB
            options.CompactionPercentage = 0.1; // LRU compaction (10%)
        });

        services.AddSwaggerGen(g =>
        {
            g.UseInlineDefinitionsForEnums();
        });
    }

    private static void AddSqLite(this IServiceCollection services)
    {
        services.AddDbContextPool<DataContext>(options =>
        {
            options.UseSqlite("Data source=config/kavita.db", builder =>
            {
                builder.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            });
            options.EnableDetailedErrors();
            options.EnableSensitiveDataLogging();
            options.ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        });
    }
}
