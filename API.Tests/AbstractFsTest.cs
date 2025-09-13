

using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using API.Services.Tasks.Scanner.Parser;

namespace API.Tests;

public abstract class AbstractFsTest
{

    protected static readonly string Root = Parser.NormalizePath(Path.GetPathRoot(Directory.GetCurrentDirectory()));
    protected static readonly string ConfigDirectory = Root + "kavita/config/";
    protected static readonly string CacheDirectory = ConfigDirectory + "cache/";
    protected static readonly string CacheLongDirectory = ConfigDirectory + "cache-long/";
    protected static readonly string CoverImageDirectory = ConfigDirectory + "covers/";
    protected static readonly string BackupDirectory = ConfigDirectory + "backups/";
    protected static readonly string LogDirectory = ConfigDirectory + "logs/";
    protected static readonly string BookmarkDirectory = ConfigDirectory + "bookmarks/";
    protected static readonly string SiteThemeDirectory = ConfigDirectory + "themes/";
    protected static readonly string TempDirectory = ConfigDirectory + "temp/";
    protected static readonly string ThemesDirectory = ConfigDirectory + "theme";
    protected static readonly string DataDirectory = Root + "data/";

    protected static MockFileSystem CreateFileSystem()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.SetCurrentDirectory(Root + "kavita/");
        fileSystem.AddDirectory(Root + "kavita/config/");
        fileSystem.AddDirectory(CacheDirectory);
        fileSystem.AddDirectory(CacheLongDirectory);
        fileSystem.AddDirectory(CoverImageDirectory);
        fileSystem.AddDirectory(BackupDirectory);
        fileSystem.AddDirectory(BookmarkDirectory);
        fileSystem.AddDirectory(SiteThemeDirectory);
        fileSystem.AddDirectory(LogDirectory);
        fileSystem.AddDirectory(TempDirectory);
        fileSystem.AddDirectory(DataDirectory);
        fileSystem.AddDirectory(ThemesDirectory);

        return fileSystem;
    }
}
