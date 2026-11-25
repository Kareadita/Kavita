using Kavita.Services.Helpers;
using NetVips;
using Xunit.Abstractions;

namespace Kavita.Services.Tests.Images;

/// <summary>
/// The primary and secondary colours picked for each sample in ColorScapes/. To look at them, uncomment the [Fact] on
/// <see cref="GenerateGallery"/>, run it, then open the colorscape.html it prints
/// </summary>
public class ColorScapeTests(ITestOutputHelper output) : AbstractImageTest
{
    private static readonly string SampleDirectory = Path.Join(ImageServiceDirectory, "ColorScapes");

    protected override string OutputDirectory { get; } = SampleDirectory;

    [Theory]
    [InlineData("blue-2.png")]
    [InlineData("blue.jpg")]
    [InlineData("green-red.png")]
    [InlineData("green.png")]
    [InlineData("lightblue-2.png")]
    [InlineData("lightblue.png")]
    [InlineData("pink.png")]
    [InlineData("yellow-blue.png")]
    public void CalculateColorScape_SameImage_SameColorsEveryTime(string fileName)
    {
        var imagePath = Path.Join(SampleDirectory, fileName);

        var results = Enumerable.Range(0, 10)
            .Select(_ => ImageHelper.CalculateColorScape(imagePath))
            .Select(c => $"{c.Primary} {c.Secondary}")
            .Distinct()
            .ToList();

        Assert.Single(results);
    }

    [Theory]
    [InlineData("blue-2.png", "#0C212C #2B5C75")]
    [InlineData("blue.jpg", "#72D2D2 #514E55")]
    [InlineData("green-red.png", "#D84D18 #557245")]
    [InlineData("green.png", "#519487 #B3AC97")]
    [InlineData("lightblue-2.png", "#3D2A40 #B68784")]
    [InlineData("lightblue.png", "#D67C72 #6F98BF")]
    [InlineData("pink.png", "#A6404A #C9726F")]
    [InlineData("yellow-blue.png", "#E4AD28 #54535E")]
    public void CalculateColorScape_KnownImage_ExactColors(string fileName, string expected)
    {
        var colors = ImageHelper.CalculateColorScape(Path.Join(SampleDirectory, fileName));

        Assert.Equal(expected, $"{colors.Primary} {colors.Secondary}");
    }

    // [Fact]
    public void GenerateGallery()
    {
        ClearOutputs();

        var rows = new List<GalleryRow>();
        foreach (var sample in GetSamples(SampleDirectory))
        {
            var colors = ImageHelper.CalculateColorScape(sample);
            var primary = WriteSwatch(colors.Primary, OutputPath(sample, "primary", ".png"));
            var secondary = WriteSwatch(colors.Secondary, OutputPath(sample, "secondary", ".png"));

            rows.Add(new GalleryRow(Path.GetFileName(sample),
            [
                Cell("Source", sample),
                Cell("Primary", primary, colors.Primary),
                Cell("Secondary", secondary, colors.Secondary),
            ]));
        }

        output.WriteLine(WriteGallery("colorscape.html", "ColorScapes", rows));
    }

    private static string WriteSwatch(string hexColor, string path)
    {
        var (r, g, b) = ImageHelper.HexToRgb(hexColor);
        using var black = Image.Black(200, 100);
        using var swatch = black.NewFromImage(r, g, b);
        swatch.WriteToFile(path);
        return path;
    }
}
