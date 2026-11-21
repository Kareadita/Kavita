using System.Text;
using Kavita.Services.Helpers;
using Kavita.Services.Tests.Helpers;
using NetVips;
using Image = NetVips.Image;

namespace Kavita.Services.Tests.Images;

[Collection(NetVipsGlobalStateCollection.Name)]
public class ImageHelperHeaderTests : IDisposable
{
    private readonly int _maxFiles = NetVips.Cache.MaxFiles;

    public ImageHelperHeaderTests()
    {
        NetVips.Cache.MaxFiles = 0;
    }

    public void Dispose()
    {
        NetVips.Cache.MaxFiles = _maxFiles;
    }

    // HEIC (HEVC in a HEIF box) is not here: the bundled libvips cannot encode HEVC, so the test cannot make one
    public static TheoryData<string, string?> Cases => new()
    {
        { "png", ".png" },
        { "jpeg", ".jpg" },
        { "webp", ".webp" },
        { "gif", ".gif" },
        { "avif", ".avif" },
        { "text", null },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void GetImageExtension_FileAndMemory_ReadTheSameFormat(string content, string? expected)
    {
        var bytes = Encode(content);
        // .bin so the name says nothing about the format
        var path = Path.Join(Path.GetTempPath(), $"kavita-image-header-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, bytes);

        try
        {
            Assert.Equal(expected, ImageHelper.GetImageExtension(path));

            var header = ImageHelper.GetImageHeader(bytes);
            Assert.Equal(expected, header?.Extension);
            if (header != null)
            {
                Assert.Equal(40, header.Width);
                Assert.Equal(30, header.Height);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] Encode(string content)
    {
        using var black = Image.Black(40, 30, bands: 3);
        using var colored = black + new double[] { 200, 80, 40 };
        using var image = colored.Cast(Enums.BandFormat.Uchar);

        return content switch
        {
            "png" => image.PngsaveBuffer(),
            "jpeg" => image.JpegsaveBuffer(),
            "webp" => image.WebpsaveBuffer(),
            "gif" => image.GifsaveBuffer(),
            "avif" => image.HeifsaveBuffer(compression: Enums.ForeignHeifCompression.Av1),
            _ => Encoding.UTF8.GetBytes("not an image"),
        };
    }
}
