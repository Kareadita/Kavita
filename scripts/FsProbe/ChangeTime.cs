using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace FsProbe;

/// <summary>
/// Change time (Linux ctime, Windows ChangeTime). Set by the OS on any content or metadata write, tools cannot set it back
/// </summary>
public static unsafe class ChangeTime
{
    private const int AtFdCwd = -100;
    private const uint StatxCtime = 0x80;
    private const int StatxCtimeOffset = 96;

    private static readonly delegate* unmanaged<int, byte*, int, uint, byte*, int> Statx = LoadStatx();

    public static bool Supported => OperatingSystem.IsWindows() || (OperatingSystem.IsLinux() && Statx != null);

    public static long? GetUtcTicks(string path)
    {
        if (OperatingSystem.IsLinux()) return Linux(path);
        if (OperatingSystem.IsWindows()) return Windows(path);
        return null;
    }

    // statx's struct is the same on every architecture, unlike stat's
    private static delegate* unmanaged<int, byte*, int, uint, byte*, int> LoadStatx()
    {
        if (!OperatingSystem.IsLinux()) return null;
        foreach (var name in new[] { "libc.so.6", "libc" })
        {
            if (NativeLibrary.TryLoad(name, out var lib) && NativeLibrary.TryGetExport(lib, "statx", out var fn))
            {
                return (delegate* unmanaged<int, byte*, int, uint, byte*, int>) fn;
            }
        }
        return null;
    }

    private static long? Linux(string path)
    {
        if (Statx == null) return null;

        var bytes = Encoding.UTF8.GetBytes(path + '\0');
        var buffer = stackalloc byte[256];
        fixed (byte* p = bytes)
        {
            if (Statx(AtFdCwd, p, 0, StatxCtime, buffer) != 0) return null;
        }
        if ((*(uint*) buffer & StatxCtime) == 0) return null;

        var seconds = *(long*) (buffer + StatxCtimeOffset);
        var nanos = *(uint*) (buffer + StatxCtimeOffset + 8);
        return DateTime.UnixEpoch.Ticks + seconds * TimeSpan.TicksPerSecond + nanos / 100;
    }

    private const uint FileReadAttributes = 0x80;
    private const uint ShareAll = 0x7;
    private const uint OpenExisting = 3;
    private const uint FlagBackupSemantics = 0x02000000;
    private const int FileBasicInfoClass = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct FileBasicInfo
    {
        public long CreationTime;
        public long LastAccessTime;
        public long LastWriteTime;
        public long ChangeTime;
        public uint FileAttributes;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security,
        uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int infoClass,
        out FileBasicInfo info, uint size);

    private static long? Windows(string path)
    {
        using var handle = CreateFileW(path, FileReadAttributes, ShareAll, IntPtr.Zero, OpenExisting,
            FlagBackupSemantics, IntPtr.Zero);
        if (handle.IsInvalid) return null;
        if (!GetFileInformationByHandleEx(handle, FileBasicInfoClass, out var info, (uint) sizeof(FileBasicInfo))) return null;
        return DateTime.FromFileTimeUtc(info.ChangeTime).Ticks;
    }
}
