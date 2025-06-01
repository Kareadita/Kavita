using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using API.DTOs.Reader;

namespace API.DTOs.Downloads;

public sealed record DownloadBookmarkDto
{
    [Required]
    public IEnumerable<BookmarkDto> Bookmarks { get; set; } = default!;
}
