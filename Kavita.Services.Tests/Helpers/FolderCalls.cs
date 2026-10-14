using System.Collections.Concurrent;
using System.IO.Abstractions;
using Kavita.Common.Extensions;
using Kavita.Services.Scanner;

namespace Kavita.Services.Tests.Helpers;

/// <summary>
/// A file system that counts, per folder inside <c>root</c>, how often a folder is listed or checked for existence,
/// through <see cref="IDirectory"/> or <see cref="IDirectoryInfo"/>
/// </summary>
public sealed class FolderCalls
{
    private static readonly HashSet<string> Listings =
    [
        nameof(IDirectory.EnumerateDirectories), nameof(IDirectory.EnumerateFiles), nameof(IDirectory.EnumerateFileSystemEntries),
        nameof(IDirectory.GetDirectories), nameof(IDirectory.GetFiles), nameof(IDirectory.GetFileSystemEntries),
        nameof(IDirectoryInfo.EnumerateFileSystemInfos), nameof(IDirectoryInfo.GetFileSystemInfos),
    ];

    private readonly string _root;

    public IFileSystem FileSystem { get; }
    public ConcurrentDictionary<string, int> ListingsByFolder { get; } = new();
    public ConcurrentDictionary<string, int> ExistsByFolder { get; } = new();

    public FolderCalls(IFileSystem inner, string root)
    {
        _root = Parser.NormalizePath(root).TrimEnd('/');

        var directory = UnreadableFolders.Forwarder<IDirectory>.Create(inner.Directory, (method, args) =>
        {
            if (args is [string path, ..])
            {
                Count(method.Name == nameof(IDirectory.Exists) ? "get_Exists" : method.Name, path);
            }
            return null;
        });

        var directoryInfo = UnreadableFolders.Forwarder<IDirectoryInfoFactory>.Create(inner.DirectoryInfo, (_, _) => null,
            (method, result) => method.Name == nameof(IDirectoryInfoFactory.New) && result is IDirectoryInfo info
                ? UnreadableFolders.Forwarder<IDirectoryInfo>.Create(info, (m, _) =>
                {
                    Count(m.Name, info.FullName);
                    return null;
                })
                : result);

        FileSystem = UnreadableFolders.Forwarder<IFileSystem>.Create(inner, (_, _) => null, (method, result) => method.Name switch
        {
            $"get_{nameof(IFileSystem.Directory)}" => directory,
            $"get_{nameof(IFileSystem.DirectoryInfo)}" => directoryInfo,
            _ => result,
        });
    }

    public void Clear()
    {
        ListingsByFolder.Clear();
        ExistsByFolder.Clear();
    }

    private void Count(string methodName, string path)
    {
        var folder = Parser.NormalizePath(path).TrimEnd('/');
        if (!folder.IsSameOrInsideFolder(_root)) return;

        if (methodName == "get_Exists")
        {
            ExistsByFolder.AddOrUpdate(folder, 1, (_, n) => n + 1);
        }
        else if (Listings.Contains(methodName))
        {
            ListingsByFolder.AddOrUpdate(folder, 1, (_, n) => n + 1);
        }
    }
}
