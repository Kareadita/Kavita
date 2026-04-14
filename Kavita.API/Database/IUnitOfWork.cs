using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Repositories;

namespace Kavita.API.Database;

public interface IUnitOfWork
{
    IDataContext DataContext { get; }
    ISeriesRepository SeriesRepository { get; }
    IUserRepository UserRepository { get; }
    ILibraryRepository LibraryRepository { get; }
    IVolumeRepository VolumeRepository { get; }
    ISettingsRepository SettingsRepository { get; }
    IAppUserProgressRepository AppUserProgressRepository { get; }
    ICollectionTagRepository CollectionTagRepository { get; }
    IChapterRepository ChapterRepository { get; }
    IReadingListRepository ReadingListRepository { get; }
    ISeriesMetadataRepository SeriesMetadataRepository { get; }
    IPersonRepository PersonRepository { get; }
    IGenreRepository GenreRepository { get; }
    ITagRepository TagRepository { get; }
    ISiteThemeRepository SiteThemeRepository { get; }
    IMangaFileRepository MangaFileRepository { get; }
    IDeviceRepository DeviceRepository { get; }
    IMediaErrorRepository MediaErrorRepository { get; }
    IScrobbleRepository ScrobbleRepository { get; }
    IUserTableOfContentRepository UserTableOfContentRepository { get; }
    IAppUserSmartFilterRepository AppUserSmartFilterRepository { get; }
    IAppUserExternalSourceRepository AppUserExternalSourceRepository { get; }
    IExternalSeriesMetadataRepository ExternalSeriesMetadataRepository { get; }
    IEmailHistoryRepository EmailHistoryRepository { get; }
    IAppUserReadingProfileRepository AppUserReadingProfileRepository { get; }
    IAnnotationRepository AnnotationRepository { get; }
    IEpubFontRepository EpubFontRepository { get; }
    IReadingSessionRepository ReadingSessionRepository { get; }
    IClientDeviceRepository ClientDeviceRepository { get; }
    IReadingListRemapRuleRepository RemapRuleRepository { get; }

    /// <summary>
    /// Commits pending changes to the database inside an IMMEDIATE transaction so writer
    /// contention waits on the SQLite writer lock (via busy_timeout) instead of failing with
    /// SQLITE_BUSY_SNAPSHOT. Optionally retries transient BUSY/LOCKED errors.
    /// </summary>
    /// <param name="maxRetries">How many extra attempts to make if a retryable SQLite busy/locked error occurs. 0 matches pre-existing behavior.</param>
    /// <param name="ct"></param>
    Task<bool> CommitAsync(int maxRetries = 0, CancellationToken ct = default);
    bool HasChanges();
    Task<bool> RollbackAsync(CancellationToken ct = default);
}
