using System.IO.Abstractions;

namespace Kavita.Services.Tests.Helpers;

/// <summary>
/// The real file system, except that the current directory is <paramref name="root"/>. <see cref="DirectoryService"/> builds
/// every folder from the current directory, so a real-disk test gets its own config folder without changing the process's
/// </summary>
public sealed class RootedFileSystem(string root) : IFileSystem
{
    private readonly FileSystem _disk = new();

    public IDirectory Directory => field ??= new RootedDirectory(this, root);
    public IDirectoryInfoFactory DirectoryInfo => _disk.DirectoryInfo;
    public IDriveInfoFactory DriveInfo => _disk.DriveInfo;
    public IFile File => _disk.File;
    public IFileInfoFactory FileInfo => _disk.FileInfo;
    public IFileStreamFactory FileStream => _disk.FileStream;
    public IFileSystemWatcherFactory FileSystemWatcher => _disk.FileSystemWatcher;
    public IFileVersionInfoFactory FileVersionInfo => _disk.FileVersionInfo;
    public IPath Path => _disk.Path;
    public IRandomAccess RandomAccess => _disk.RandomAccess;

    private sealed class RootedDirectory(IFileSystem fileSystem, string root) : DirectoryWrapper(fileSystem)
    {
        public override string GetCurrentDirectory() => root;
    }
}
