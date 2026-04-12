using System.ComponentModel;

namespace Kavita.Models.Entities.Enums.UserPreferences;

public enum KeyBindTarget
{
    [Description(nameof(NavigateToSettings))]
    NavigateToSettings = 0,

    [Description(nameof(OpenSearch))]
    OpenSearch = 1,

    [Description(nameof(NavigateToScrobbling))]
    NavigateToScrobbling = 2,

    [Description(nameof(ToggleFullScreen))]
    ToggleFullScreen = 3,

    [Description(nameof(BookmarkPage))]
    BookmarkPage = 4,

    [Description(nameof(OpenHelp))]
    OpenHelp = 5,

    [Description(nameof(GoTo))]
    GoTo = 6,

    [Description(nameof(ToggleMenu))]
    ToggleMenu = 7,

    [Description(nameof(PageLeft))]
    PageLeft = 8,

    [Description(nameof(PageRight))]
    PageRight = 9,

    [Description(nameof(Escape))]
    Escape = 10,

    [Description(nameof(PageUp))]
    PageUp = 11,

    [Description(nameof(PageDown))]
    PageDown = 12,

    [Description(nameof(OffsetDoublePage))]
    OffsetDoublePage = 13,

    [Description(nameof(NextChapter))]
    NextChapter = 14,

    [Description(nameof(PreviousChapter))]
    PreviousChapter = 15,

    [Description(nameof(FirstPage))]
    FirstPage = 16,

    [Description(nameof(LastPage))]
    LastPage = 17,
}
