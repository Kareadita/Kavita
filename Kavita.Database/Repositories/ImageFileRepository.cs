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
            .Where(f => f.Kind != ImageKind.Bookmark)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public IAsyncEnumerable<List<ImageFile>> GetImageFilesForDeletionAsync(int batchSize, CancellationToken ct = default)
    {
        return context.ImageFile
            .Where(f => f.Kind != ImageKind.Favicon && f.Kind != ImageKind.Publisher && f.Kind != ImageKind.Bookmark)
            .WhereNotReferenced(context)
            .BatchByKeyAsync(f => f.Id, batchSize, ct);
    }

    public IAsyncEnumerable<List<ImageFile>> GetImageFilesForReencodeAsync(ImageKind kind, EncodeFormat format, ImageBox? box,
        int batchSize, CancellationToken ct = default)
    {
        var query = context.ImageFile
            .Where(f => f.Kind == kind && f.Format != null);

        if (box is { } b)
        {
            var width = b.Width;
            var height = b.Height;
            query = query.Where(f => f.Format != format || f.Width > width || f.Height > height);
        }
        else
        {
            query = query.Where(f => f.Format != format);
        }

        return query.BatchByKeyAsync(f => f.Id, batchSize, ct);
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
}
