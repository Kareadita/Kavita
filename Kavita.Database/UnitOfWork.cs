using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.Database.Repositories;
using Kavita.Models.Entities.User;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database;


public class UnitOfWork : IUnitOfWork
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;
    private readonly UserManager<AppUser> _userManager;

    public UnitOfWork(DataContext context, IMapper mapper, UserManager<AppUser> userManager)
    {
        _context = context;
        _mapper = mapper;
        _userManager = userManager;

        SeriesRepository = new SeriesRepository(_context, _mapper);
        UserRepository = new UserRepository(_context, _userManager, _mapper);
        LibraryRepository = new LibraryRepository(_context, _mapper);
        VolumeRepository = new VolumeRepository(_context, _mapper);
        SettingsRepository = new SettingsRepository(_context, _mapper);
        AppUserProgressRepository = new AppUserProgressRepository(_context, _mapper);
        CollectionTagRepository = new CollectionTagRepository(_context, _mapper);
        ChapterRepository = new ChapterRepository(_context, _mapper);
        ReadingListRepository = new ReadingListRepository(_context, _mapper);
        SeriesMetadataRepository = new SeriesMetadataRepository(_context);
        PersonRepository = new PersonRepository(_context, _mapper);
        GenreRepository = new GenreRepository(_context, _mapper);
        TagRepository = new TagRepository(_context, _mapper);
        SiteThemeRepository = new SiteThemeRepository(_context, _mapper);
        MangaFileRepository = new MangaFileRepository(_context);
        DeviceRepository = new DeviceRepository(_context, _mapper);
        MediaErrorRepository = new MediaErrorRepository(_context, _mapper);
        ScrobbleRepository = new ScrobbleRepository(_context, _mapper);
        UserTableOfContentRepository = new UserTableOfContentRepository(_context, _mapper);
        AppUserSmartFilterRepository = new AppUserSmartFilterRepository(_context, _mapper);
        AppUserExternalSourceRepository = new AppUserExternalSourceRepository(_context, _mapper);
        ExternalSeriesMetadataRepository = new ExternalSeriesMetadataRepository(_context, _mapper);
        EmailHistoryRepository = new EmailHistoryRepository(_context, _mapper);
        AppUserReadingProfileRepository = new AppUserReadingProfileRepository(_context, _mapper);
        AnnotationRepository = new AnnotationRepository(_context, _mapper);
        EpubFontRepository = new EpubFontRepository(_context, _mapper);
        ReadingSessionRepository = new ReadingSessionRepository(_context, _mapper);
        ClientDeviceRepository = new ClientDeviceRepository(_context, _mapper);
        RemapRuleRepository = new ReadingListRemapRuleRepository(_context, _mapper);
    }

    /// <summary>
    /// This is here for Scanner only. Don't use otherwise.
    /// </summary>
    public IDataContext DataContext => _context;
    public ISeriesRepository SeriesRepository { get; }
    public IUserRepository UserRepository { get; }
    public ILibraryRepository LibraryRepository { get; }
    public IVolumeRepository VolumeRepository { get; }
    public ISettingsRepository SettingsRepository { get; }
    public IAppUserProgressRepository AppUserProgressRepository { get; }
    public ICollectionTagRepository CollectionTagRepository { get; }
    public IChapterRepository ChapterRepository { get; }
    public IReadingListRepository ReadingListRepository { get; }
    public ISeriesMetadataRepository SeriesMetadataRepository { get; }
    public IPersonRepository PersonRepository { get; }
    public IGenreRepository GenreRepository { get; }
    public ITagRepository TagRepository { get; }
    public ISiteThemeRepository SiteThemeRepository { get; }
    public IMangaFileRepository MangaFileRepository { get; }
    public IDeviceRepository DeviceRepository { get; }
    public IMediaErrorRepository MediaErrorRepository { get; }
    public IScrobbleRepository ScrobbleRepository { get; }
    public IUserTableOfContentRepository UserTableOfContentRepository { get; }
    public IAppUserSmartFilterRepository AppUserSmartFilterRepository { get; }
    public IAppUserExternalSourceRepository AppUserExternalSourceRepository { get; }
    public IExternalSeriesMetadataRepository ExternalSeriesMetadataRepository { get; }
    public IEmailHistoryRepository EmailHistoryRepository { get; }
    public IAppUserReadingProfileRepository AppUserReadingProfileRepository { get; }
    public IAnnotationRepository AnnotationRepository { get; }
    public IEpubFontRepository EpubFontRepository { get;  }
    public IReadingSessionRepository ReadingSessionRepository { get;  }
    public IClientDeviceRepository ClientDeviceRepository { get; }
    public IReadingListRemapRuleRepository RemapRuleRepository { get; }

    /// <summary>
    /// Commits pending changes inside an IMMEDIATE SQLite transaction so writer contention
    /// waits on the writer lock (via busy_timeout) instead of failing with SQLITE_BUSY_SNAPSHOT.
    /// </summary>
    public async Task<bool> CommitAsync(CancellationToken ct = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var result = await _context.SaveChangesAsync(ct) > 0;
        await tx.CommitAsync(ct);
        return result;
    }

    /// <summary>
    /// Is the DB Context aware of Changes in loaded entities
    /// </summary>
    /// <returns></returns>
    public bool HasChanges()
    {
        return _context.ChangeTracker.HasChanges();
    }

    /// <summary>
    /// Rollback transaction
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<bool> RollbackAsync(CancellationToken ct = default)
    {
        try
        {
            await _context.Database.RollbackTransactionAsync(ct);
        }
        catch (Exception)
        {
            // Swallow exception (this might be used in places where a transaction isn't setup)
        }

        return true;
    }
}
