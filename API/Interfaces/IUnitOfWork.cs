﻿using System.Threading.Tasks;
using API.Interfaces.Repositories;

namespace API.Interfaces
{
    public interface IUnitOfWork
    {
        ISeriesRepository SeriesRepository { get; }
        IUserRepository UserRepository { get; }
        ILibraryRepository LibraryRepository { get; }
        IVolumeRepository VolumeRepository { get; }
        ISettingsRepository SettingsRepository { get; }
        IAppUserProgressRepository AppUserProgressRepository { get; }
        ICollectionTagRepository CollectionTagRepository { get; }
        IFileRepository FileRepository { get; }
        IChapterRepository ChapterRepository { get; }
        bool Commit();
        Task<bool> CommitAsync();
        bool HasChanges();
        bool Rollback();
        Task<bool> RollbackAsync();
    }
}
