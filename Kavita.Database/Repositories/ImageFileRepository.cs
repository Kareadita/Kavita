using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Repositories;
using Kavita.Models.Entities;
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
}
