using Kavita.Models.Entities.Enums;
using Kavita.Models.Extensions;
using Kavita.Models.Images;
using Kavita.Services.Images;
using Microsoft.Extensions.Logging;
using NetVips;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests.Images;

/// <summary>
/// Manual check for what <see cref="ImagePipeline.Reencode"/> does to real images from the dev config folder.
/// Uncomment the [Fact] on <see cref="GenerateGallery"/>, run it, then open the reencode.html it prints
/// </summary>
public class ReencodeComparison(ITestOutputHelper output) : AbstractImageTest
{
    private static readonly ImagePolicy Policy = new(EncodeFormat.WEBP, CoverImageSize.Default);

    protected override string OutputDirectory { get; } = Path.Join(ImageServiceDirectory, "Reencode");

    // [Fact]
    public void GenerateGallery()
    {
        var config = Path.GetFullPath(Path.Join(AppContext.BaseDirectory, "../../../../Kavita.Server/config"));
        var (boxWidth, boxHeight) = Policy.CoverSize.GetDimensions();
        var coverSamples = PickCovers(Path.Join(config, "covers"), [
            new CoverWanted("Large cover", ImageKind.SeriesCover, s => s.Width > boxWidth && s.Width < s.Height),
            new CoverWanted("Cover already in the box", ImageKind.ChapterCover, s => s.Width <= boxWidth && s.Height <= boxHeight),
        ]);

        var samples = coverSamples.Append(Largest("Favicon", ImageKind.Favicon, Path.Join(config, "favicons")))
            .Append(Largest("Publisher", ImageKind.Publisher, Path.Join(config, "images", "publishers")))
            .OfType<Sample>()
            .ToList();

        ClearOutputs();
        var pipeline = new ImagePipeline(Substitute.For<ILogger<ImagePipeline>>());
        var rows = samples.Select((s, i) => Reencode(pipeline, s, i)).ToList();

        output.WriteLine(WriteGallery("reencode.html", "Reencode comparison", rows));
    }

    private GalleryRow Reencode(ImagePipeline pipeline, Sample sample, int index)
    {
        var before = OutputPath($"{index}", "before", Path.GetExtension(sample.Path));
        File.Copy(sample.Path, before, overwrite: true);

        var after = Path.GetFileName(OutputPath($"{index}", "after", Policy.Format.ForKind(sample.Kind).GetExtension()));
        pipeline.Reencode(before, sample.Kind, Policy, OutputDirectory, after);
        var box = ImageProfile.For(sample.Kind).GetBox(Policy);
        var boxNote = box is { } b ? $"box {b.Width}x{b.Height}" : "no box, kept at source size";

        return new GalleryRow($"{sample.Name} ({Path.GetFileName(sample.Path)}, {sample.Kind}, {boxNote})",
        [
            Cell("Before", before, Ratio(before), box?.Width, box?.Height),
            Cell("After Reencode", Path.Join(OutputDirectory, after), Ratio(Path.Join(OutputDirectory, after)), box?.Width, box?.Height),
        ]);
    }

    private List<Sample?> PickCovers(string directory, List<CoverWanted> wanted)
    {
        var found = new Sample?[wanted.Count];
        foreach (var path in Directory.EnumerateFiles(directory))
        {
            if (found.All(f => f != null)) break;
            if (ReadSize(path) is not { } size) continue;

            for (var i = 0; i < wanted.Count; i++)
            {
                if (found[i] == null && wanted[i].Match(size))
                {
                    found[i] = new Sample(wanted[i].Name, wanted[i].Kind, path);
                }
            }
        }

        foreach (var missing in wanted.Where((_, i) => found[i] == null))
        {
            output.WriteLine($"{missing.Name}: no matching file in {directory}");
        }

        return found.ToList();
    }

    private static Sample? Largest(string name, ImageKind kind, string directory)
    {
        if (!Directory.Exists(directory)) return null;

        var file = Directory.EnumerateFiles(directory)
            .Select(f => (Path: f, Size: ReadSize(f)))
            .Where(f => f.Size != null)
            .MaxBy(f => f.Size!.Value.Width * f.Size!.Value.Height);
        return file.Path == null ? null : new Sample(name, kind, file.Path);
    }

    private static ImageBox? ReadSize(string path)
    {
        try
        {
            using var header = Image.NewFromFile(path, access: Enums.Access.Sequential);
            return new ImageBox(header.Width, header.Height);
        }
        catch (VipsException)
        {
            return null;
        }
    }

    private static string Ratio(string path)
    {
        var info = Measure(path);
        return $"ratio {(double) info.Width / info.Height:0.000}";
    }

    private sealed record Sample(string Name, ImageKind Kind, string Path);

    private sealed record CoverWanted(string Name, ImageKind Kind, Func<ImageBox, bool> Match);
}
