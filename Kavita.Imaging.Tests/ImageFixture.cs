using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Kavita.Imaging.Tests;

/// <summary>
/// Generates a small set of deterministic, known-property images on disk so the
/// behaviour suite can assert relative outcomes (identical, higher-res, color vs grayscale)
/// without committing binary assets. Shared across a test class via IClassFixture.
/// </summary>
public sealed class ImageFixture : IDisposable
{
    private readonly string _root;

    /// <summary>A colorful 200x200 image.</summary>
    public string ColorfulA { get; }
    /// <summary>A byte-identical copy of <see cref="ColorfulA"/>.</summary>
    public string ColorfulACopy { get; }
    /// <summary>A grayscale 200x200 image (same structure as <see cref="ColorfulA"/>, desaturated).</summary>
    public string Grayscale { get; }
    /// <summary>A colorful 600x600 image (>3x the pixel count of the low-res one).</summary>
    public string HighRes { get; }
    /// <summary>A colorful 100x100 image.</summary>
    public string LowRes { get; }
    /// <summary>A path that does not exist on disk.</summary>
    public string Missing { get; }

    public ImageFixture()
    {
        _root = Path.Combine(Path.GetTempPath(), "KavitaImagingTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        ColorfulA = WriteColorful(200, 200, "colorful-a.png");
        ColorfulACopy = WriteColorful(200, 200, "colorful-a-copy.png");
        Grayscale = WriteGrayscale(200, 200, "grayscale.png");
        HighRes = WriteColorful(600, 600, "high-res.png");
        LowRes = WriteColorful(100, 100, "low-res.png");
        Missing = Path.Combine(_root, "does-not-exist.png");
    }

    /// <summary>
    /// Writes a deterministic colorful gradient. R/G/B vary independently across the image
    /// so channels differ well beyond the colorfulness threshold.
    /// </summary>
    private string WriteColorful(int width, int height, string name)
    {
        using var image = new Image<Rgba32>(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var r = (byte)(x * 255 / width);
                var g = (byte)(y * 255 / height);
                var b = (byte)((x + y) * 255 / (width + height));
                image[x, y] = new Rgba32(r, g, b);
            }
        }

        var path = Path.Combine(_root, name);
        image.SaveAsPng(path);
        return path;
    }

    /// <summary>
    /// Writes a deterministic grayscale gradient (R == G == B for every pixel).
    /// </summary>
    private string WriteGrayscale(int width, int height, string name)
    {
        using var image = new Image<Rgba32>(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var v = (byte)((x + y) * 255 / (width + height));
                image[x, y] = new Rgba32(v, v, v);
            }
        }

        var path = Path.Combine(_root, name);
        image.SaveAsPng(path);
        return path;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup of temp files; ignore failures.
        }
    }
}
