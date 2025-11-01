using System.ComponentModel;

namespace API.Entities.Enums.UserPreferences;

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
}
