using System.ComponentModel;

namespace Kavita.Models.Entities.Enums.ReadingList;

public enum ReadingListProvider
{
    /// <summary>
    /// Default, List created within Kavita. No Sync capabilities
    /// </summary>
    [Description("File")]
    None = 0,
    /// <summary>
    /// Created by File upload. No Sync capabilities
    /// </summary>
    [Description("File")]
    File = 1,
    /// <summary>
    /// Downloaded via CBL Manager or direct Url feed
    /// </summary>
    [Description("Url")]
    Url = 2,
}
