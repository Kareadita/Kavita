using System.ComponentModel;

namespace Kavita.Models.Images;

/// <summary>
/// What an image written through <c>IImagePipeline</c> is for. The kind picks the <see cref="ImageProfile"/>, so every
/// callsite writing the same kind gets the same sizing. A new member needs a case in <see cref="ImageProfile.For"/>,
/// or the write throws
/// </summary>
public enum ImageKind
{
    /// <summary>
    /// Chapter cover, extracted from the file, uploaded, or downloaded from a URL. Sized to the server's cover size
    /// </summary>
    [Description("Chapter Cover")]
    ChapterCover = 0,
    /// <summary>
    /// Volume cover, uploaded or downloaded from a URL. Sized to the server's cover size
    /// </summary>
    [Description("Volume Cover")]
    VolumeCover = 1,
    /// <summary>
    /// Series cover, uploaded or downloaded from a URL. Sized to the server's cover size
    /// </summary>
    [Description("Series Cover")]
    SeriesCover = 2,
    /// <summary>
    /// Person cover, downloaded from a URL. Sized to the server's cover size
    /// </summary>
    [Description("Person Cover")]
    PersonCover = 3,
    /// <summary>
    /// A user's profile image, downloaded from a URL. Sized to the server's cover size
    /// </summary>
    [Description("User Cover")]
    UserCover = 4,
    /// <summary>
    /// Reading list cover, uploaded or set by a CBL import. Sized to the server's cover size
    /// </summary>
    [Description("Reading List Cover")]
    ReadingListCover = 5,
    /// <summary>
    /// Collection cover, uploaded. Sized to the server's cover size
    /// </summary>
    [Description("Collection Cover")]
    CollectionCover = 6,
    /// <summary>
    /// Custom icon uploaded for a library. Fit within a small fixed box
    /// </summary>
    [Description("Library Icon")]
    LibraryIcon = 7,
    /// <summary>
    /// A website's favicon. Fit within a small fixed box
    /// </summary>
    [Description("Favicon")]
    Favicon = 8,
    /// <summary>
    /// Publisher logo. Fit within a fixed box
    /// </summary>
    [Description("Publisher")]
    Publisher = 9,
    /// <summary>
    /// A bookmarked reader page. Kept at the source size, only re-encoded
    /// </summary>
    [Description("Bookmark")]
    Bookmark = 10,
    /// <summary>
    /// Thumbnail of a reader page. Sized to the server's cover size
    /// </summary>
    [Description("Reader Page Thumbnail")]
    ReaderPageThumb = 11,
}
