using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Kavita.Models.DTOs.Reader;

namespace Kavita.Models.DTOs.Downloads;

public sealed record DownloadBookmarkDto
{
    [Required]
    public IEnumerable<BookmarkDto> Bookmarks { get; set; } = default!;
}
