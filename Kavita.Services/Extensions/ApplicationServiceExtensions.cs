using System.IO.Abstractions;
using Kavita.API.Services;
using Kavita.API.Services.Helpers;
using Kavita.API.Services.Metadata;
using Kavita.API.Services.Plus;
using Kavita.API.Services.Reading;
using Kavita.API.Services.ReadingLists;
using Kavita.API.Services.Scanner;
using Kavita.API.Services.SignalR;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Helpers;
using Kavita.Services.HostedServices;
using Kavita.Services.Metadata;
using Kavita.Services.Plus;
using Kavita.Services.Plus.ScrobbleService;
using Kavita.Services.Reading;
using Kavita.Services.ReadingLists;
using Kavita.Services.Scanner;
using Kavita.Services.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Kavita.Services.Extensions;

public static class ApplicationServiceExtensions
{

    public static void AddKavitaServices(this IServiceCollection services)
    {
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
        services.AddScoped<IRatingService, RatingService>();
        services.AddScoped<IPersonService, PersonService>();
        services.AddScoped<IReadingProfileService, ReadingProfileService>();
        services.AddScoped<IKoreaderService, KoreaderService>();
        services.AddScoped<IFontService, FontService>();
        services.AddScoped<IAnnotationService, AnnotationService>();
        services.AddScoped<IOpdsService, OpdsService>();
        services.AddScoped<IOAuthService, OAuthService>();

        services.AddScoped<IUrlValidationService, UrlValidationService>();

        services.AddScoped<ICblExportService, CblExportService>();
        services.AddScoped<ICblGithubService, CblGithubService>();
        services.AddScoped<ICblImportService, CblImportService>();

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
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IAuthKeyService, AuthKeyService>();

        services.AddScoped<IKavitaPlusApiService, KavitaPlusApiService>();
        services.AddKeyedScoped<IScrobbleProviderService, MangabakaScrobbleProviderService>(ScrobbleProvider.MangaBaka);
        services.AddKeyedScoped<IScrobbleProviderService, AniListScrobbleProviderService>(ScrobbleProvider.AniList);
        services.AddKeyedScoped<IScrobbleProviderService, MyAnimeListScrobbleProviderService>(ScrobbleProvider.Mal);
        services.AddKeyedScoped<IScrobbleProviderService, HardcoverScrobbleProviderService>(ScrobbleProvider.Hardcover);
        services.AddScoped<IScrobbleRuleService, ScrobbleRuleService>();
        services.AddScoped<IScrobblingService, ScrobblingService>();
        services.AddScoped<ILicenseService, LicenseService>();
        services.AddScoped<IExternalMetadataService, ExternalMetadataService>();
        services.AddScoped<ISmartCollectionSyncService, SmartCollectionSyncService>();
        services.AddScoped<IWantToReadSyncService, WantToReadSyncService>();
        services.AddScoped<IKavitaPlusAuditService, KavitaPlusAuditService>();
        services.AddScoped<IKavitaPlusProviderHealthService, KavitaPlusProviderHealthService>();

        services.AddScoped<IOidcService, OidcService>();


        services.AddScoped<IReadingHistoryService, ReadingHistoryService>();
        services.AddScoped<IClientDeviceService, ClientDeviceService>();
        services.AddScoped<IDeviceTrackingService, DeviceTrackingService>();


        services.AddScoped<IFileCacheService, FileCacheService>();
        services.AddSingleton<IReadingSessionService, ReadingSessionService>();
        services.AddSingleton<IEntityNamingService, EntityNamingService>();
        services.AddSingleton<ActiveUserTrackerService>(); // This is required for the below lines. It allows IHostedService.StopAsync() to be called on shutdown
        services.AddSingleton<IActiveUserTrackerService>(sp => sp.GetRequiredService<ActiveUserTrackerService>());
        services.AddHostedService(sp => sp.GetRequiredService<ActiveUserTrackerService>());

        services.AddHostedService<StartupTasksHostedService>();
    }

}
