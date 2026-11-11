using System.Text.RegularExpressions;
using Kavita.Services.Scanner;
using Kavita.Services.Tests.Helpers;
using NetVips;
using Image = NetVips.Image;

namespace Kavita.Services.Tests.Images;

[Collection(NetVipsGlobalStateCollection.Name)]
public class NetVipsBlockUntrustedTests
{
    public static TheoryData<string> CoverImageExtensions()
    {
        var data = new TheoryData<string>();
        foreach (Match match in Regex.Matches(Parser.ImageFileExtensions, @"\\\.(\w+)"))
        {
            data.Add(match.Groups[1].Value);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CoverImageExtensions))]
    public void BlockUntrusted_CoverFormatStillLoads(string extension)
    {
        using var source = Image.Black(64, 64, bands: 3);
        var buffer = source.WriteToBuffer("." + extension);

        NetVips.NetVips.BlockUntrusted = true;
        try
        {
            using var decoded = Image.NewFromBuffer(buffer);
            decoded.Avg();
            Assert.Equal(64, decoded.Width);
        }
        finally
        {
            NetVips.NetVips.BlockUntrusted = false;
        }
    }
}
