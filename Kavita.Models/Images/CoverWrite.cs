using Kavita.Models.Entities.Enums;

namespace Kavita.Models.Images;

#nullable enable

/// <summary>
/// How <c>IImageFileService.SetCoverAsync</c> writes a cover
/// </summary>
/// <param name="Origin">Stored on the <see cref="Entities.ImageFile"/> row. Later jobs decide by it whether they may replace
/// the cover: the nightly rebuild only touches <see cref="ImageOrigin.Composite"/></param>
/// <param name="Lock">The lock the entity ends with, not a request to lock. <c>false</c> unlocks an entity that was
/// locked; pass the entity's current <c>CoverImageLocked</c> to keep it as it is. Unlocking a chapter first gives any
/// locked volume or series sharing its cover a copy of its own</param>
/// <param name="ChooseBetter">Keep the current cover when it is the better image (higher resolution, then color and
/// quality). Nothing is written and the lock is left as it was. Needs an <see cref="ImageSource.FromFile"/></param>
/// <param name="RejectPlaceholder">Skip the write when the source matches AniList's "no image" placeholder. Needs an
/// <see cref="ImageSource.FromFile"/></param>
public sealed record CoverWrite(
    ImageOrigin Origin,
    bool Lock,
    bool ChooseBetter = false,
    bool RejectPlaceholder = false);
