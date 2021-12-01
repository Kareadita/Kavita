using System;
using System.IO.Abstractions;
using API.Extensions;

namespace API.Services;

public interface IFileService
{
    bool HasFileBeenModifiedSince(string filePath, DateTime time);
    bool Exists(string filePath);
}

public class FileService : IFileService
{
    private readonly IFileSystem  _fileSystem;

    public FileService(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public FileService() : this(fileSystem: new FileSystem()) { }

    /// <summary>
    /// If the File on disk's last modified time is after passed time
    /// </summary>
    /// <remarks>This has a resolution to the minute. Will ignore seconds and milliseconds</remarks>
    /// <param name="filePath">Full qualified path of file</param>
    /// <param name="time"></param>
    /// <returns></returns>
    public bool HasFileBeenModifiedSince(string filePath, DateTime time)
    {
        return _fileSystem.File.GetLastWriteTime(filePath).Truncate(TimeSpan.TicksPerMinute) > time.Truncate(TimeSpan.TicksPerMinute);
    }

    public bool Exists(string filePath)
    {
        return _fileSystem.File.Exists(filePath);
    }
}
