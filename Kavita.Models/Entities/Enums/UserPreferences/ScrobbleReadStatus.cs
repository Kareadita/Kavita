using System.ComponentModel;

namespace Kavita.Models.Entities.Enums.UserPreferences;

public enum ScrobbleReadStatus
{
    /// <summary>
    /// A no-action read status
    /// </summary>
    [Description("Ignore")]
    Ignore = 0,
    [Description("Want to Read")]
    WantToRead = 1,
    [Description("Read")]
    Read = 2,
    [Description("Un read")]
    UnRead = 3,
    [Description("Dropped")]
    Dropped = 4,
    [Description("On hold")]
    OnHold = 5,
}
