using System.Collections.Generic;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Reader;
using Kavita.Models.Entities.User;

namespace Kavita.API.Services;

public interface IBookmarkService
{
    Task DeleteBookmarkFiles(IEnumerable<AppUserBookmark> bookmarks);
    Task<bool> BookmarkPage(AppUser userWithBookmarks, BookmarkDto bookmarkDto, string imageToBookmark);
    Task<bool> RemoveBookmarkPage(AppUser userWithBookmarks, BookmarkDto bookmarkDto);
    Task<IEnumerable<string>> GetBookmarkFilesById(IEnumerable<int> bookmarkIds);
}
