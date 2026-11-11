using System.ComponentModel;

namespace Kavita.Models.Images;

public enum ImageKind
{
    [Description("Chapter Cover")]
    ChapterCover = 0,
    [Description("Volume Cover")]
    VolumeCover = 1,
    [Description("Series Cover")]
    SeriesCover = 2,
    [Description("Person Cover")]
    PersonCover = 3,
    [Description("User Cover")]
    UserCover = 4,
    [Description("Reading List Cover")]
    ReadingListCover = 5,
    [Description("Collection Cover")]
    CollectionCover = 6,
    [Description("Library Icon")]
    LibraryIcon = 7,
    [Description("Favicon")]
    Favicon = 8,
    [Description("Publisher")]
    Publisher = 9,
    [Description("Bookmark")]
    Bookmark = 10,
    [Description("Reader Page Thumbnail")]
    ReaderPageThumb = 11,
}
