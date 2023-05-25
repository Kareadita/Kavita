using System;
using System.Threading.Tasks;
using API.Data.Repositories;
using API.Entities;
using AutoMapper;
using Microsoft.AspNetCore.Identity;

namespace API.Data;

public interface IUnitOfWork
{
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
    IScrobbleEventRepository ScrobbleEventRepository { get; }
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
    }

    public ISeriesRepository SeriesRepository => new SeriesRepository(_context, _mapper);
    public IUserRepository UserRepository => new UserRepository(_context, _userManager, _mapper);
    public ILibraryRepository LibraryRepository => new LibraryRepository(_context, _mapper);

    public IVolumeRepository VolumeRepository => new VolumeRepository(_context, _mapper);

    public ISettingsRepository SettingsRepository => new SettingsRepository(_context, _mapper);

    public IAppUserProgressRepository AppUserProgressRepository => new AppUserProgressRepository(_context, _mapper);
    public ICollectionTagRepository CollectionTagRepository => new CollectionTagRepository(_context, _mapper);
    public IChapterRepository ChapterRepository => new ChapterRepository(_context, _mapper);
    public IReadingListRepository ReadingListRepository => new ReadingListRepository(_context, _mapper);
    public ISeriesMetadataRepository SeriesMetadataRepository => new SeriesMetadataRepository(_context);
    public IPersonRepository PersonRepository => new PersonRepository(_context, _mapper);
    public IGenreRepository GenreRepository => new GenreRepository(_context, _mapper);
    public ITagRepository TagRepository => new TagRepository(_context, _mapper);
    public ISiteThemeRepository SiteThemeRepository => new SiteThemeRepository(_context, _mapper);
    public IMangaFileRepository MangaFileRepository => new MangaFileRepository(_context);
    public IDeviceRepository DeviceRepository => new DeviceRepository(_context, _mapper);
    public IMediaErrorRepository MediaErrorRepository => new MediaErrorRepository(_context, _mapper);
    public IScrobbleEventRepository ScrobbleEventRepository => new ScrobbleEventRepository(_context, _mapper);

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
