using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using API.DTOs.Reader;

namespace API.DTOs.Downloads;

public class DownloadBookmarkDto
{
    [Required]
    public IEnumerable<BookmarkDto> Bookmarks { get; set; } = default!;
}
