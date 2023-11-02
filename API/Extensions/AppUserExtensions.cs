using System.Collections.Generic;
using System.Linq;
using API.Entities;
using API.Helpers;

namespace API.Extensions;
#nullable enable

public static class AppUserExtensions
{
    /// <summary>
    /// Adds a new SideNavStream to the user's SideNavStreams. This user should have these streams already loaded
    /// </summary>
    /// <param name="user"></param>
    /// <param name="library"></param>
    public static void CreateSideNavFromLibrary(this AppUser user, Library library)
    {
        user.SideNavStreams ??= new List<AppUserSideNavStream>();
        var maxCount = user.SideNavStreams.Select(s => s.Order).DefaultIfEmpty().Max();

        if (user.SideNavStreams.FirstOrDefault(s => s.LibraryId == library.Id) != null) return;

        user.SideNavStreams.Add(new AppUserSideNavStream()
        {
            Name = library.Name,
            Order = maxCount + 1,
            IsProvided = false,
            StreamType = SideNavStreamType.Library,
            LibraryId = library.Id,
            Visible = true,
        });
    }


    public static void RemoveSideNavFromLibrary(this AppUser user, Library library)
    {
        user.SideNavStreams ??= new List<AppUserSideNavStream>();

        // Find the library and remove it
        var item = user.SideNavStreams.FirstOrDefault(s => s.LibraryId == library.Id);
        if (item == null) return;
        user.SideNavStreams.Remove(item);

        OrderableHelper.ReorderItems(user.SideNavStreams);

    }
}
