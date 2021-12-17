using System;
using System.Threading.Tasks;
using API.Services;

namespace API.Data;

/// <summary>
/// Responsible to migrate existing bookmarks to files
/// </summary>
public static class MigrateBookmarks
{
    public static Task Migrate(IDirectoryService directoryService)
    {
        Console.WriteLine("Checking if migration of bookmarks needed");

        return Task.CompletedTask;
    }
}
