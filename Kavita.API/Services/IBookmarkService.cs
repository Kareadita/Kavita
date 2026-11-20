using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Kavita.Models.DTOs.Reader;
using Kavita.Models.Entities.User;

namespace Kavita.API.Services;

public interface IBookmarkService
{
    Task<bool> BookmarkPage(AppUser userWithBookmarks, BookmarkDto bookmarkDto, string imageToBookmark, CancellationToken ct = default);
    Task<bool> RemoveBookmarkPage(AppUser userWithBookmarks, BookmarkDto bookmarkDto, CancellationToken ct = default);
    Task<IEnumerable<string>> GetBookmarkFilesById(int userId, int seriesId, IEnumerable<int> bookmarkIds, CancellationToken ct = default);
    /// <summary>
    /// Deletes the bookmarks and their image rows in one commit, then their files and any folders left empty
    /// </summary>
    /// <param name="bookmarks">Tracked, with ImageFile loaded</param>
    /// <returns>False when the commit failed, nothing is deleted then</returns>
    Task<bool> RemoveBookmarksAsync(IList<AppUserBookmark> bookmarks, CancellationToken ct = default);
    /// <summary>
    /// Moves every bookmark file from the BookmarkDirectory setting's folder to <paramref name="to"/>, then points the setting at it.
    /// A file whose name is already taken in <paramref name="to"/> stays where it is
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 2 * 60 * 60), AutomaticRetry(Attempts = 0)]
    Task MoveBookmarkDirectory(string to, CancellationToken ct = default);
}
