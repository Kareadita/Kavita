using System.Collections.Generic;
using System.Linq;
using API.Data;
using API.Entities;
using Kavita.Common;

namespace API.Helpers.Builders;
#nullable enable

public class AppUserBuilder : IEntityBuilder<AppUser>
{
    private readonly AppUser _appUser;
    public AppUser Build() => _appUser;

    public AppUserBuilder(string username, string email, SiteTheme? theme = null)
    {
        _appUser = new AppUser()
        {
            UserName = username,
            Email = email,
            ApiKey = HashUtil.ApiKey(),
            UserPreferences = new AppUserPreferences
            {
                Theme = theme ?? Seed.DefaultThemes.First()
            },
            ReadingLists = new List<ReadingList>(),
            Bookmarks = new List<AppUserBookmark>(),
            Libraries = new List<Library>(),
            Ratings = new List<AppUserRating>(),
            Progresses = new List<AppUserProgress>(),
            Devices = new List<Device>(),
            Id = 0,
            DashboardStreams = new List<AppUserDashboardStream>(),
            SideNavStreams = new List<AppUserSideNavStream>()
        };
    }

    public AppUserBuilder WithLibrary(Library library, bool createSideNavStream = false)
    {
        _appUser.Libraries.Add(library);
        if (!createSideNavStream) return this;

        if (library.Id != 0 && _appUser.SideNavStreams.Any(s => s.LibraryId == library.Id)) return this;
        _appUser.SideNavStreams.Add(new AppUserSideNavStream()
        {
            Name = library.Name,
            IsProvided = false,
            Visible = true,
            LibraryId = library.Id,
            StreamType = SideNavStreamType.Library,
            Order = _appUser.SideNavStreams.Max(s => s.Order) + 1,
        });

        return this;
    }


    public AppUserBuilder WithLocale(string locale)
    {
        _appUser.UserPreferences.Locale = locale;
        return this;
    }

    public AppUserBuilder WithRole(string role)
    {
        _appUser.UserRoles ??= new List<AppUserRole>();
        _appUser.UserRoles.Add(new AppUserRole() {Role = new AppRole() {Name = role}});
        return this;
    }
}
