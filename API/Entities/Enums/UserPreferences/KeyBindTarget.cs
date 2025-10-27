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
}
