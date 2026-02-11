using System.Linq;
using Kavita.Models.Entities;
using Kavita.Models.Entities.User;
using Kavita.Models.Helpers;

namespace Kavita.Models.Extensions;

public static class AppUserExtensions
{
    /// <param name="user"></param>
    extension(AppUser user)
    {
        /// <summary>
        /// Adds a new SideNavStream to the user's SideNavStreams. This user should have these streams already loaded
        /// </summary>
        /// <param name="library"></param>
        public void CreateSideNavFromLibrary(Library library)
        {
            var maxCount = user.SideNavStreams.Select(s => s.Order).DefaultIfEmpty().Max();

            if (user.SideNavStreams.FirstOrDefault(s => s.LibraryId == library.Id) != null) return;

            user.SideNavStreams.Add(new AppUserSideNavStream
            {
                Name = library.Name,
                Order = maxCount + 1,
                IsProvided = false,
                StreamType = SideNavStreamType.Library,
                LibraryId = library.Id,
                Visible = true,
            });
        }

        public void RemoveSideNavFromLibrary(Library library)
        {
            // Find the library and remove it
            var item = user.SideNavStreams.FirstOrDefault(s => s.LibraryId == library.Id);
            if (item == null) return;
            user.SideNavStreams.Remove(item);

            OrderableHelper.ReorderItems(user.SideNavStreams);
        }

        public AgeRestriction GetAgeRestriction()
        {
            return new AgeRestriction
            {
                AgeRating = user.AgeRestriction,
                IncludeUnknowns = user.AgeRestrictionIncludeUnknowns,
            };
        }
    }
}
