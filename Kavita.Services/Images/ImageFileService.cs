using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Images;

namespace Kavita.Services.Images;

public class ImageFileService : IImageFileService
{
    public Task SetCoverAsync(IHasCoverImageFile entity, ImageSource source, CoverWrite write, bool commit = true,
        CancellationToken ct = default)
    {
        throw new System.NotImplementedException();
    }

    public Task ShareCoverAsync(IHasCoverImageFile target, IHasCoverImageFile source, bool lockBoth, bool commit = true,
        CancellationToken ct = default)
    {
        throw new System.NotImplementedException();
    }

    public Task ClearCoverAsync(IHasCoverImageFile entity, bool commit = true, CancellationToken ct = default)
    {
        throw new System.NotImplementedException();
    }

    public Task UpdateLockAsync(IHasLockableCoverImageFile entity, bool locked, bool commit = true,
        CancellationToken ct = default)
    {
        throw new System.NotImplementedException();
    }
}
