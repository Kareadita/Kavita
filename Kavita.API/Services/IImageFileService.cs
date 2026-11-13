using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Images;

namespace Kavita.API.Services;

public interface IImageFileService
{
    Task SetCoverAsync(IHasCoverImageFile entity, ImageSource source, CoverWrite write, bool commit = true,
        CancellationToken ct = default);

    Task ShareCoverAsync(IHasCoverImageFile target, IHasCoverImageFile source, bool lockBoth, bool commit = true,
        CancellationToken ct = default);

    Task ClearCoverAsync(IHasCoverImageFile entity, bool commit = true, CancellationToken ct = default);
    Task UpdateLockAsync(IHasLockableCoverImageFile entity, bool locked, bool commit = true, CancellationToken ct = default);
}
