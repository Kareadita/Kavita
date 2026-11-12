using System;

namespace Kavita.Models.Images;

/// <summary>
/// How an <see cref="ImageKind"/> is sized.
/// <br/> <see cref="ImageSizeRule.CoverSize"/> reads the server's cover size from <see cref="ImagePolicy"/>.
/// <br/> <see cref="ImageSizeRule.FitWithin"/> shrinks into <see cref="Width"/> x <see cref="Height"/>
/// keeping the aspect ratio and never upscales.
/// <br/> <see cref="KeepsTransparency"/> kinds are never written in a format without an alpha channel (aka JPEG wont save, PNG would be used)
/// </summary>
public sealed record ImageProfile(ImageSizeRule Size, int Width = 0, int Height = 0, bool KeepsTransparency = false)
{
    private static readonly ImageProfile Cover = new(ImageSizeRule.CoverSize);
    private static readonly ImageProfile LibraryIcon = new(ImageSizeRule.FitWithin, 64, 64, KeepsTransparency: true);
    private static readonly ImageProfile Favicon = new(ImageSizeRule.FitWithin, 32, 32, KeepsTransparency: true);
    private static readonly ImageProfile Publisher = new(ImageSizeRule.FitWithin, 128, 128, KeepsTransparency: true);
    private static readonly ImageProfile Bookmark = new(ImageSizeRule.Original);

    /// <summary>
    /// The sizing profile every write of <paramref name="kind"/> uses
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The kind has no profile yet</exception>
    public static ImageProfile For(ImageKind kind) => kind switch
    {
        ImageKind.ChapterCover or ImageKind.VolumeCover or ImageKind.SeriesCover or ImageKind.PersonCover
            or ImageKind.UserCover or ImageKind.ReadingListCover or ImageKind.CollectionCover
            or ImageKind.ReaderPageThumb => Cover,
        ImageKind.LibraryIcon => LibraryIcon,
        ImageKind.Favicon => Favicon,
        ImageKind.Publisher => Publisher,
        ImageKind.Bookmark => Bookmark,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
