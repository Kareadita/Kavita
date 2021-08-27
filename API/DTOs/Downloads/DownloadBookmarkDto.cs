using System.Collections.Generic;

namespace API.DTOs.Downloads
{
    public class DownloadBookmarkDto
    {
        public IEnumerable<BookmarkDto> Bookmarks { get; set; }
    }
}
