using System;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.ReadingLists;
#nullable enable

public sealed record ReadingListItemDto
{
    public int Id { get; init; }
    public int Order { get; init; }
    public int ChapterId { get; init; }
    public int SeriesId { get; init; }
    public string? SeriesName { get; set; }
    public string? SeriesSortName { get; set; }
    public MangaFormat SeriesFormat { get; set; }
    public int PagesRead { get; set; }

    [Obsolete("Use Chapter.Pages instead")]
    public int PagesTotal { get; set; }

    [Obsolete("Use Chapter.Range instead")]
    public string? ChapterNumber { get; set; }

    [Obsolete("Use Volume.Name instead")]
    public string? VolumeNumber { get; set; }

    [Obsolete("Use Chapter.TitleName instead")]
    public string? ChapterTitleName { get; set; }

    public int VolumeId { get; set; }
    public int LibraryId { get; set; }
    public string? Title { get; set; }
    public LibraryType LibraryType { get; set; }
    public string? LibraryName { get; set; }

    /// <summary>
    /// Release Date from Chapter
    /// </summary>
    [Obsolete("Use Chapter.ReleaseDate instead")]
    public DateTime? ReleaseDate { get; set; }

    /// <summary>
    /// Used internally only
    /// </summary>
    public int ReadingListId { get; set; }
    /// <summary>
    /// The last time a reading list item (underlying chapter) was read by current authenticated user
    /// </summary>
    public DateTime? LastReadingProgressUtc { get; set; }
    /// <summary>
    /// File size of underlying item
    /// </summary>
    /// <remarks>This is only used for CDisplayEx</remarks>
    [Obsolete("Use Chapter.Pages or file data instead")]
    public long FileSize { get; set; }
    /// <summary>
    /// The chapter summary
    /// </summary>
    [Obsolete("Use Chapter.Summary instead")]
    public string? Summary { get; set; }

    [Obsolete("Use Chapter.IsSpecial instead")]
    public bool IsSpecial { get; set; }

    /// <summary>
    /// Nested chapter metadata
    /// </summary>
    public ReadingListItemChapterDto Chapter { get; set; } = null!;

    /// <summary>
    /// Nested volume metadata
    /// </summary>
    public ReadingListItemVolumeDto Volume { get; set; } = null!;

}
