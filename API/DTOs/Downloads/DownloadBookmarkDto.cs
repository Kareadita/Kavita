using System.Collections.Generic;
using API.DTOs.Reader;

namespace API.DTOs.Downloads
{
    public class DownloadBookmarkDto
    {
        public IEnumerable<BookmarkDto> Bookmarks { get; set; }
    }
}
