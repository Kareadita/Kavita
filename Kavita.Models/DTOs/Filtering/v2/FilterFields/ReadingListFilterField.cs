namespace Kavita.Models.DTOs.Filtering.v2.FilterFields;

public enum ReadingListFilterField
{
    Title = 1,
    ReleaseYear = 2,
    ItemCount = 3,
    Tags = 4,
    Writer = 5,
    Artist = 6,
    /// <summary>
    /// Source is either Kavita/Url/File
    /// </summary>
    Provider = 7,
    MissingItemCount = 8
}
