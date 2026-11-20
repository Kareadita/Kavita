using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Repositories;
using Kavita.Database.Extensions;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Images;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;

public class ImageFileRepository(DataContext context) : IImageFileRepository
{
    public void Add(ImageFile file)
    {
        context.ImageFile.Add(file);
    }

    public void Remove(ImageFile file)
    {
        context.ImageFile.Remove(file);
    }

    public async Task<ImageFile?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await context.ImageFile
            .FirstOrDefaultAsync(f => f.Id == id, ct);
    }

    public async Task<ImageFile?> GetByNameAsync(ImageKind kind, string fileName, CancellationToken ct = default)
    {
        return await context.ImageFile
            .FirstOrDefaultAsync(f => f.Kind == kind && f.FileName == fileName, ct);
    }

    public async Task<ImageFile?> GetByKeyAsync(ImageKind kind, string key, CancellationToken ct = default)
    {
        return await context.ImageFile
            .FirstOrDefaultAsync(f => f.Kind == kind && f.Key == key, ct);
    }

    public async Task<ImageFileHolders> GetHoldersAsync(int imageFileId, CancellationToken ct = default)
    {
        var chapters = await context.Chapter
            .Where(c => c.CoverImageFileId == imageFileId)
            .ToListAsync(ct);
        var volumes = await context.Volume
            .Where(v => v.CoverImageFileId == imageFileId)
            .ToListAsync(ct);
        var series = await context.Series
            .Where(s => s.CoverImageFileId == imageFileId)
            .ToListAsync(ct);

        return new ImageFileHolders(chapters, volumes, series);
    }

    public async Task<IList<ImageFile>> GetForBackupAsync(CancellationToken ct = default)
    {
        return await context.ImageFile
            .Where(f => f.Origin != ImageOrigin.Scan && f.Origin != ImageOrigin.KavitaPlus)
            .Where(f => f.Kind != ImageKind.Bookmark) // Bookmarks are backed up via a dedicated method
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public IAsyncEnumerable<List<ImageFile>> GetImageFilesForDeletionAsync(int batchSize, CancellationToken ct = default)
    {
        return GetImageFilesForDeletionQuery().BatchByKeyAsync(f => f.Id, batchSize, ct);
    }

    public async Task<int> CountImageFilesForDeletionAsync(CancellationToken ct = default)
    {
        return await GetImageFilesForDeletionQuery().CountAsync(ct);
    }

    public IAsyncEnumerable<List<ImageFile>> GetImageFilesForReencodeAsync(ImageKind kind, EncodeFormat format, ImageBox? box,
        int batchSize, CancellationToken ct = default)
    {
        return GetImageFilesForReencodeQuery(kind, format, box).BatchByKeyAsync(f => f.Id, batchSize, ct);
    }

    public async Task<int> CountImageFilesForReencodeAsync(ImageKind kind, EncodeFormat format, ImageBox? box,
        CancellationToken ct = default)
    {
        return await GetImageFilesForReencodeQuery(kind, format, box).CountAsync(ct);
    }

    private IQueryable<ImageFile> GetImageFilesForDeletionQuery()
    {
        var keptKinds = ImageProfile.GetKeptKinds();
        return context.ImageFile
            .Where(f => !keptKinds.Contains(f.Kind) && f.Kind != ImageKind.Favicon && f.Kind != ImageKind.Publisher)
            .WhereNotReferenced(context);
    }

    private IQueryable<ImageFile> GetImageFilesForReencodeQuery(ImageKind kind, EncodeFormat format, ImageBox? box)
    {
        var query = context.ImageFile
            .Where(f => f.Kind == kind && f.Format != null);

        if (box is { } b)
        {
            var width = b.Width;
            var height = b.Height;
            return query.Where(f => f.Format != format || f.Width > width || f.Height > height);
        }

        return query.Where(f => f.Format != format);
    }

    public async Task<IList<IHasCoverImageFile>> GetAllHoldersAsync(IList<int> imageFileIds, CancellationToken ct = default)
    {
        // TODO: See if running in parallel can speed this up
        var holders = new List<IHasCoverImageFile>();
        holders.AddRange(await context.Chapter.Where(e => imageFileIds.Contains(e.CoverImageFileId!.Value)).ToListAsync(ct));
        holders.AddRange(await context.Volume.Where(e => imageFileIds.Contains(e.CoverImageFileId!.Value)).ToListAsync(ct));
        holders.AddRange(await context.Series.Where(e => imageFileIds.Contains(e.CoverImageFileId!.Value)).ToListAsync(ct));
        holders.AddRange(await context.Library.Where(e => imageFileIds.Contains(e.CoverImageFileId!.Value)).ToListAsync(ct));
        holders.AddRange(await context.Person.Where(e => imageFileIds.Contains(e.CoverImageFileId!.Value)).ToListAsync(ct));
        holders.AddRange(await context.ReadingList.Where(e => imageFileIds.Contains(e.CoverImageFileId!.Value)).ToListAsync(ct));
        holders.AddRange(await context.Users.Where(e => imageFileIds.Contains(e.CoverImageFileId!.Value)).ToListAsync(ct));
        holders.AddRange(await context.AppUserCollection.Where(e => imageFileIds.Contains(e.CoverImageFileId!.Value)).ToListAsync(ct));

        return holders;
    }

    #region Migration Only

    public async Task<bool> HasImageFilesNeedingMigrationAsync(CancellationToken ct = default)
    {
        return await GetImageFilesNeedingMigrationQuery().AnyAsync(ct);
    }

    public async Task<int> CountImageFilesNeedingMigrationAsync(CancellationToken ct = default)
    {
        return await GetImageFilesNeedingMigrationQuery().CountAsync(ct);
    }

    public IAsyncEnumerable<List<ImageFile>> GetImageFilesNeedingMigrationAsync(ImageKind kind, int batchSize, CancellationToken ct = default)
    {
        return GetImageFilesNeedingMigrationQuery()
            .Where(f => f.Kind == kind)
            .BatchByKeyAsync(f => f.Id, batchSize, ct);
    }

    public async Task<IList<string>> GetFileNamesAsync(IList<ImageKind> kinds, CancellationToken ct = default)
    {
        return await context.ImageFile
            .Where(f => kinds.Contains(f.Kind))
            .Select(f => f.FileName)
            .ToListAsync(ct);
    }

    public async Task<IList<ImageFile>> GetKeyedImageFilesWithDuplicateKeysAsync(CancellationToken ct = default)
    {
        var keyed = context.ImageFile
            .Where(f => (f.Kind == ImageKind.Favicon || f.Kind == ImageKind.Publisher) && f.Key != null);

        var duplicated = keyed
            .GroupBy(f => new { f.Kind, f.Key })
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        return await keyed
            .Where(f => duplicated.Any(d => d.Kind == f.Kind && d.Key == f.Key))
            .ToListAsync(ct);
    }

    private IQueryable<ImageFile> GetImageFilesNeedingMigrationQuery()
    {
        return context.ImageFile.Where(f => f.Format == null);
    }

    #endregion
}
