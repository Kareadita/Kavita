using System.Collections.Generic;
using System.Linq;
using API.Entities;
using API.Entities.Enums;
using SQLitePCL;

namespace API.Helpers.Builders;

public class LibraryBuilder : IEntityBuilder<Library>
{
    private readonly Library _library;
    public Library Build() => _library;

    public LibraryBuilder(string name, LibraryType type = LibraryType.Manga)
    {
        _library =  new Library()
        {
            Name = name,
            Type = type,
            Series = new List<Series>(),
            Folders = new List<FolderPath>(),
            AppUsers = new List<AppUser>()
        };
    }

    public LibraryBuilder WithFolderPath(FolderPath folderPath)
    {
        _library.Folders ??= new List<FolderPath>();
        if (_library.Folders.All(f => f != folderPath)) _library.Folders.Add(folderPath);
        return this;
    }

    public LibraryBuilder WithSeries(Series series)
    {
        _library.Series ??= new List<Series>();
        _library.Series.Add(series);
        return this;
    }

    public LibraryBuilder WithAppUser(AppUser appUser)
    {
        _library.AppUsers ??= new List<AppUser>();
        _library.AppUsers.Add(appUser);
        return this;
    }
}
