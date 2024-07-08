using System.ComponentModel;

namespace API.Entities.Enums.UserPreferences;

public enum PdfLayoutMode
{
    /// <summary>
    /// Multiple pages render stacked (normal pdf experience)
    /// </summary>
    [Description("Multiple")]
    Multiple = 0,
    // [Description("Single")]
    // Single = 1,
    /// <summary>
    /// A book mode where page turns are animated and layout is side-by-side
    /// </summary>
    [Description("Book")]
    Book = 2,
    // [Description("Infinite Scroll")]
    // InfiniteScroll = 3
}
