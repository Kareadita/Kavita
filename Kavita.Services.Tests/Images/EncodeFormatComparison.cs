using NetVips;
using Xunit.Abstractions;

namespace Kavita.Services.Tests.Images;

/// <summary>
/// Manual check for how converting a stored image between formats changes it, on files from the dev config folder.
/// Uncomment the [Fact] on <see cref="GenerateGallery"/>, run it, then open the compare.html it prints
/// </summary>
public class EncodeFormatComparison(ITestOutputHelper output) : AbstractImageTest
{
    protected override string OutputDirectory { get; } = Path.Join(ImageServiceDirectory, "EncodeFormat");

    // Every panel gets a fixed viewport and one drag pans them all, so the same pixels line up at 800%
    private const string SyncedPanScript = """
        const panStyle = document.createElement('style');
        panStyle.textContent = '.frame { width: 22rem; height: 70vh; overflow: hidden; cursor: grab; } .frame img { position: absolute; left: 0; top: 0; }';
        document.head.appendChild(panStyle);
        let panX = 0, panY = 0, pan = null;
        const movePanels = () => { for (const img of document.querySelectorAll('.frame img')) img.style.translate = `${panX}px ${panY}px`; };
        document.addEventListener('pointerdown', e => { if (!e.target.closest('.frame')) return; pan = { sx: e.clientX - panX, sy: e.clientY - panY }; e.preventDefault(); });
        window.addEventListener('pointermove', e => { if (!pan) return; panX = e.clientX - pan.sx; panY = e.clientY - pan.sy; movePanels(); });
        window.addEventListener('pointerup', () => pan = null);
        document.getElementById('sample').addEventListener('change', movePanels);
        """;

    //[Fact]
    public void GenerateGallery()
    {
        var config = Path.GetFullPath(Path.Join(AppContext.BaseDirectory, "../../../../Kavita.Server/config"));
        var largestBookmark = Directory.GetFiles(Path.Join(config, "bookmarks"), "*.webp", SearchOption.AllDirectories)
            .OrderByDescending(f => new FileInfo(f).Length)
            .First();
        var sources = new Dictionary<string, string>
        {
            ["bookmark"] = largestBookmark,
            ["cover"] = Path.Join(config, "covers", "v1_c1.webp"),
        };

        ClearOutputs();
        var rows = sources.Select(s => Convert(s.Key, s.Value)).ToList();

        output.WriteLine(WriteGallery("compare.html", "Encode comparison", rows, SyncedPanScript));
    }

    private GalleryRow Convert(string name, string sourcePath)
    {
        var source = OutputPath(name, "source", ".webp");
        var png = OutputPath(name, "png", ".png");
        var roundTrip = OutputPath(name, "roundtrip", ".webp");
        var jpeg = OutputPath(name, "jpeg", ".jpg");
        File.Copy(sourcePath, source, overwrite: true);

        using (var image = Image.NewFromFile(source)) image.WriteToFile(png);
        using (var image = Image.NewFromFile(png)) image.WriteToFile(roundTrip);
        using (var image = Image.NewFromFile(source)) image.Jpegsave(jpeg, background: [255, 255, 255]);

        using var original = Image.NewFromFile(source);
        return new GalleryRow($"{name} ({Path.GetFileName(sourcePath)})",
        [
            Cell("Source WebP", source),
            Cell("Converted to PNG", png, Difference(original, png)),
            Cell("PNG back to WebP", roundTrip, Difference(original, roundTrip)),
            Cell("Converted to JPEG", jpeg, Difference(original, jpeg)),
        ]);
    }

    /// <summary>
    /// Per channel, 0 to 255, against the source
    /// </summary>
    private static string Difference(Image original, string convertedPath)
    {
        using var converted = Image.NewFromFile(convertedPath);
        using var difference = (original - converted).Abs();
        return $"max diff {difference.Max()}, mean {difference.Avg():0.000}";
    }
}
