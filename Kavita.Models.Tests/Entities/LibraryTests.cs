using Kavita.Models.Builders;
using Kavita.Models.Entities;

namespace Kavita.Models.Tests.Entities;

public class LibraryTests
{
    [Fact]
    public void SyncFolders_KeepsUnchangedRows()
    {
        var kept = new FolderPath { Id = 1, Path = "/manga/" };
        var library = new LibraryBuilder("Manga").Build();
        library.Folders = [kept, new FolderPath { Id = 2, Path = "/comics" }];

        var changed = library.SyncFolders(["/manga", "/books"]);

        Assert.True(changed);
        Assert.Equal(2, library.Folders.Count);
        Assert.Same(kept, library.Folders.Single(f => f.Id == 1));
        Assert.Contains(library.Folders, f => f is { Id: 0, Path: "/books" });
    }

    [Fact]
    public void SyncFolders_SameFolders_ReportsNoChange()
    {
        var library = new LibraryBuilder("Manga").Build();
        library.Folders = [new FolderPath { Id = 1, Path = "/manga" }];

        Assert.False(library.SyncFolders(["/manga/", "/manga"]));
        Assert.Single(library.Folders);
    }
}
