using System;
using System.Threading.Tasks;
using API.Data.Repositories;
using API.Entities;
using AutoMapper;
using Microsoft.AspNetCore.Identity;

namespace API.Data;

public interface IUnitOfWork
{
    DataContext DataContext { get; }
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
    bool Commit();
    Task<bool> CommitAsync();
    bool HasChanges();
    Task<bool> RollbackAsync();
}

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

        SeriesRepository = new SeriesRepository(_context, _mapper, _userManager);
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
    }

    /// <summary>
    /// This is here for Scanner only. Don't use otherwise.
    /// </summary>
    public DataContext DataContext => _context;
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

    /// <summary>
    /// Commits changes to the DB. Completes the open transaction.
    /// </summary>
    /// <returns></returns>
    public bool Commit()
    {
        return _context.SaveChanges() > 0;
    }
    /// <summary>
    /// Commits changes to the DB. Completes the open transaction.
    /// </summary>
    /// <returns></returns>
    public async Task<bool> CommitAsync()
    {
        return await _context.SaveChangesAsync() > 0;
    }

    /// <summary>
    /// Is the DB Context aware of Changes in loaded entities
    /// </summary>
    /// <returns></returns>
    public bool HasChanges()
    {
        return _context.ChangeTracker.HasChanges();
    }

    public async Task BeginTransactionAsync()
    {
        await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        await _context.Database.CommitTransactionAsync();
    }

    /// <summary>
    /// Rollback transaction
    /// </summary>
    /// <returns></returns>
    public async Task<bool> RollbackAsync()
    {
        try
        {
            await _context.Database.RollbackTransactionAsync();
        }
        catch (Exception)
        {
            // Swallow exception (this might be used in places where a transaction isn't setup)
        }

        return true;
    }
}
