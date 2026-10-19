using Kavita.Services.Scanner;

namespace Kavita.Services.Tests;

public class MountDetectorServiceTests
{
    // Mount lines from Horo's Unraid Docker container (watchprobe reports, 2026-10-14 to 2026-10-18), plus a mergerfs and an escaped space
    private const string ProcMounts = """
        overlay / overlay rw,relatime,lowerdir=/var/lib/docker/overlay2/l/ABC,upperdir=/var/lib/docker/overlay2/x/diff 0 0
        proc /proc proc rw,nosuid,nodev,noexec,relatime 0 0
        cache/appdata /kavita/config zfs rw,noatime,xattr,posixacl,casesensitive 0 0
        //192.168.100.2/Media /library cifs ro,relatime,vers=3.1.1,cache=strict,username=horo,uid=99,forceuid,gid=100,forcegid,addr=192.168.100.2 0 0
        //192.168.100.2/Media /temp-test cifs rw,relatime,vers=3.1.1,cache=strict,username=horo,uid=99,forceuid,gid=100,forcegid,addr=192.168.100.2 0 0
        shfs /mnt/user fuse.shfs rw,nosuid,nodev,noatime,user_id=0,group_id=0,allow_other 0 0
        /dev/sdb1 /mnt/my\040books ext4 rw,relatime 0 0
        """;

    [Fact]
    public void Cifs_IsNetwork()
    {
        var mount = MountDetectorService.FindMount(ProcMounts, "/library/Manga-Scans-Cleaned");

        Assert.NotNull(mount);
        Assert.Equal("/library", mount.Point);
        Assert.Equal("cifs", mount.Type);
        Assert.True(mount.IsNetwork);
    }

    [Fact]
    public void Zfs_IsNotNetwork()
    {
        var mount = MountDetectorService.FindMount(ProcMounts, "/kavita/config/wp-test");

        Assert.NotNull(mount);
        Assert.Equal("zfs", mount.Type);
        Assert.False(mount.IsNetwork);
    }

    [Fact]
    public void Fuse_IsNetwork()
    {
        Assert.True(MountDetectorService.FindMount(ProcMounts, "/mnt/user/Manga")!.IsNetwork);
    }

    [Fact]
    public void LongestMountPointWins()
    {
        var mount = MountDetectorService.FindMount(ProcMounts, "/temp-test/Series A");

        Assert.Equal("/temp-test", mount!.Point);
    }

    /// <summary>
    /// systemd automount lists its autofs placeholder first and the real mount on the same point after it (Morsok's WSL, 2026-10-19)
    /// </summary>
    [Fact]
    public void StackedOnSameMountPoint_LastWins()
    {
        const string automount = """
            systemd-1 /mnt/Media autofs rw,relatime,fd=43,pgrp=1,timeout=0,minproto=5,maxproto=5,direct,pipe_ino=210 0 0
            //192.168.1.166/Media /mnt/Media cifs rw,relatime,vers=3.0,cache=strict,actimeo=1 0 0
            """;

        var mount = MountDetectorService.FindMount(automount, "/mnt/Media/Comics");

        Assert.Equal("cifs", mount!.Type);
        Assert.True(mount.IsNetwork);
    }

    [Fact]
    public void FolderOutsideEveryMount_FallsToRoot()
    {
        var mount = MountDetectorService.FindMount(ProcMounts, "/manga");

        Assert.Equal("/", mount!.Point);
        Assert.Equal("overlay", mount.Type);
    }

    [Fact]
    public void PrefixOfAnotherName_IsNotInside()
    {
        Assert.Equal("/", MountDetectorService.FindMount(ProcMounts, "/libraryOther/Series")!.Point);
    }

    [Fact]
    public void EscapedSpaceInMountPoint()
    {
        var mount = MountDetectorService.FindMount(ProcMounts, "/mnt/my books/Series A");

        Assert.Equal("/mnt/my books", mount!.Point);
        Assert.Equal("ext4", mount.Type);
    }

    [Fact]
    public void Fingerprint_HasNoSourceText_AndIsSharedByOneShare()
    {
        var library = MountDetectorService.FindMount(ProcMounts, "/library/Manga")!;
        var temp = MountDetectorService.FindMount(ProcMounts, "/temp-test")!;

        Assert.DoesNotContain("192.168", library.Fingerprint);
        Assert.Equal(64, library.Fingerprint.Length);
        Assert.Equal(library.Fingerprint, temp.Fingerprint);
    }
}
