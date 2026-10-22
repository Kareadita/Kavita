using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Kavita.Models.Scanner;

/// <param name="Point">Where the file system is mounted, e.g. /library</param>
/// <param name="Source">What is mounted, e.g. //192.168.100.2/Media</param>
/// <param name="Type">File system type, e.g. cifs</param>
public sealed record MountInfo(string Point, string Source, string Type)
{
    // cifs, smb and nfs, Docker Desktop host mounts (9p, virtiofs, fakeowner), macOS' afpfs, webdav and FUSE (macfuse,
    // osxfuse before macFUSE 4), and every Linux fuse.* (mergerfs, rclone, sshfs)
    private static readonly string[] NetworkTypes =
        ["cifs", "smb3", "smbfs", "nfs", "nfs4", "9p", "virtiofs", "fakeowner", "afpfs", "webdav", "macfuse", "osxfuse"];

    /// <summary>
    /// File system events cannot see changes other machines make on this mount, so it is polled
    /// </summary>
    public bool IsNetwork => NetworkTypes.Contains(Type) || Type.StartsWith("fuse.", StringComparison.Ordinal);

    /// <summary>
    /// SHA-256 of source and type, so a share's address is never stored. Every folder on one share has the same one
    /// </summary>
    public string Fingerprint => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{Source} {Type}")));
}
