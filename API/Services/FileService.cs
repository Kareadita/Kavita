using System;
using System.IO;
using System.IO.Abstractions;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Text;
using System.Text.Unicode;
using API.Extensions;

namespace API.Services;

public interface IFileService
{
    IFileSystem GetFileSystem();
    bool HasFileBeenModifiedSince(string filePath, DateTime time);
    bool Exists(string filePath);
    bool ValidateSha(string filepath, string sha);
}

public class FileService : IFileService
{
    private readonly IFileSystem _fileSystem;

    public FileService(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public FileService() : this(fileSystem: new FileSystem()) { }

    public IFileSystem GetFileSystem()
    {
        return _fileSystem;
    }

    /// <summary>
    /// If the File on disk's last modified time is after passed time
    /// </summary>
    /// <remarks>This has a resolution to the minute. Will ignore seconds and milliseconds</remarks>
    /// <param name="filePath">Full qualified path of file</param>
    /// <param name="time"></param>
    /// <returns></returns>
    public bool HasFileBeenModifiedSince(string filePath, DateTime time)
    {
        return !string.IsNullOrEmpty(filePath) && _fileSystem.File.GetLastWriteTime(filePath).Truncate(TimeSpan.TicksPerMinute) > time.Truncate(TimeSpan.TicksPerMinute);
    }

    public bool Exists(string filePath)
    {
        return _fileSystem.File.Exists(filePath);
    }

    /// <summary>
    /// Validates the Sha256 hash matches
    /// </summary>
    /// <param name="filepath"></param>
    /// <param name="sha"></param>
    /// <returns></returns>
    public bool ValidateSha(string filepath, string sha)
    {
        if (!Exists(filepath)) return false;
        if (string.IsNullOrEmpty(sha)) throw new ArgumentException("Sha cannot be null");

        using var fs = _fileSystem.File.OpenRead(filepath);
        fs.Position = 0;

        using var reader = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var content = reader.ReadToEnd();

        // Compute SHA hash
        var checksum = SHA256.HashData(Encoding.UTF8.GetBytes(content));

        return BitConverter.ToString(checksum).Replace("-", string.Empty).Equals(sha);

    }
}
