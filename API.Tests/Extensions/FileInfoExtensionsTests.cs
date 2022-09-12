using System;
using System.Globalization;
using System.IO;
using API.Extensions;
using Xunit;

namespace API.Tests.Extensions;

public class FileInfoExtensionsTests
{
    private static readonly string TestDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Extensions/Test Data/");

    [Fact]
    public void HasFileBeenModifiedSince_ShouldBeFalse()
    {
        var filepath = Path.Join(TestDirectory, "not modified.txt");
        var date = new FileInfo(filepath).LastWriteTime;
        Assert.False(new FileInfo(filepath).HasFileBeenModifiedSince(date));
        File.ReadAllText(filepath);
        Assert.False(new FileInfo(filepath).HasFileBeenModifiedSince(date));
    }

    [Fact]
    public void HasFileBeenModifiedSince_ShouldBeTrue()
    {
        var filepath = Path.Join(TestDirectory, "modified on run.txt");
        var date = new FileInfo(filepath).LastWriteTime;
        Assert.False(new FileInfo(filepath).HasFileBeenModifiedSince(date));
        File.AppendAllLines(filepath, new[] { DateTime.Now.ToString(CultureInfo.InvariantCulture) });
        Assert.True(new FileInfo(filepath).HasFileBeenModifiedSince(date));
    }
}
