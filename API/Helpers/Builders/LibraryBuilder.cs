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
            AppUsers = new List<AppUser>(),
            AllowScrobbling = type is LibraryType.LightNovel or LibraryType.Manga,
            LibraryFileTypes = new List<LibraryFileTypeGroup>()
        };

        _library.LibraryFileTypes.Add(new LibraryFileTypeGroup()
        {
            FileTypeGroup = FileTypeGroup.Archive
        });
        _library.LibraryFileTypes.Add(new LibraryFileTypeGroup()
        {
            FileTypeGroup = FileTypeGroup.Epub
        });
        _library.LibraryFileTypes.Add(new LibraryFileTypeGroup()
        {
            FileTypeGroup = FileTypeGroup.Images
        });
        _library.LibraryFileTypes.Add(new LibraryFileTypeGroup()
        {
            FileTypeGroup = FileTypeGroup.Pdf
        });
    }

    public LibraryBuilder(Library library)
    {
        _library =  library;
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

    public LibraryBuilder WithFolders(List<FolderPath> folders)
    {
        _library.Folders = folders;
        return this;
    }

    public LibraryBuilder WithFolderWatching(bool folderWatching)
    {
        _library.FolderWatching = folderWatching;
        return this;
    }

    public LibraryBuilder WithIncludeInDashboard(bool toInclude)
    {
        _library.IncludeInDashboard = toInclude;
        return this;
    }

    public LibraryBuilder WithIncludeInRecommended(bool toInclude)
    {
        _library.IncludeInRecommended = toInclude;
        return this;
    }

    public LibraryBuilder WithManageCollections(bool toInclude)
    {
        _library.ManageCollections = toInclude;
        return this;
    }

    public LibraryBuilder WithManageReadingLists(bool toInclude)
    {
        _library.ManageReadingLists = toInclude;
        return this;
    }

    public LibraryBuilder WithAllowMetadataMatching(bool allow)
    {
        _library.AllowMetadataMatching = allow;
        return this;
    }

    public LibraryBuilder WithAllowScrobbling(bool allowScrobbling)
    {
        _library.AllowScrobbling = allowScrobbling;
        return this;
    }
}
