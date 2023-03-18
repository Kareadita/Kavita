using System.Collections.Generic;
using System.Linq;
using API.Data;
using API.Entities;
using Kavita.Common;

namespace API.Helpers.Builders;

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
            Id = 0
        };
    }

    public AppUserBuilder WithLibrary(Library library)
    {
        _appUser.Libraries.Add(library);
        return this;
    }
}
