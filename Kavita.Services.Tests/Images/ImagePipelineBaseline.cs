using System.Diagnostics;
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
/// Manual time and memory baseline for <see cref="ImagePipeline"/>. Skipped by default: set <see cref="Manual"/> to null
/// locally, then run one scenario per dotnet test call (peak working set never resets inside a process).
/// Results are appended to Test Data/ImageService/Pipeline/baseline_output.csv
/// </summary>
/// <remarks>This is an AI generated test setup for the pipeline refactor. AI will run and parse memory/speed as I refactor to ensure no performance
/// regressions occur. </remarks>
public class ImagePipelineBaseline(ITestOutputHelper output)
{
    private const string? Manual = null; // TODO: Turn off by setting to a random string; "Manual baseline, see the class summary"

    private const int KoboWidth = 355;
    private const int KoboHeight = 530;
    private const int KoboQuality = 85;

    private static readonly string TestData =
        Path.GetFullPath(Path.Join(Directory.GetCurrentDirectory(), "../../../Test Data/ImageService"));
    private static readonly string CoversDirectory = Path.Join(TestData, "Covers");
    private static readonly string OutputDirectory = Path.Join(TestData, "Pipeline");
    private static readonly string ExpectedDirectory = Path.Join(OutputDirectory, "Expected");
    private static readonly string LargePage = Path.Join(OutputDirectory, "large-page_output.jpg");
    private static readonly ImagePolicy Policy = new(EncodeFormat.WEBP, CoverImageSize.Default);

    private readonly ImagePipeline _pipeline = new(Substitute.For<ILogger<ImagePipeline>>());

    [Fact(Skip = Manual)]
    public void Scenario1_WriteLargePage_File()
    {
        var source = EnsureLargePage();
        Measure("1 Write large page, file", 10, i =>
            _pipeline.Write(new ImageSource.FromFile(source), ImageKind.ChapterCover, Policy, OutputDirectory, $"s1-file-{i}_output.webp"));
    }

    [Fact(Skip = Manual)]
    public void Scenario1_WriteLargePage_Stream()
    {
        var source = EnsureLargePage();
        Measure("1 Write large page, stream", 10, i =>
        {
            using var stream = File.OpenRead(source);
            _pipeline.Write(new ImageSource.FromStream(stream), ImageKind.ChapterCover, Policy, OutputDirectory, $"s1-stream-{i}_output.webp");
        });
    }

    /// <summary>
    /// Also writes Pipeline/Expected/&lt;sample&gt;.expected.webp, the framing test's expected output. Commit those
    /// </summary>
    [Fact(Skip = Manual)]
    public void Scenario2_WriteEveryCoverSample()
    {
        var samples = CoverSamples();
        Directory.CreateDirectory(ExpectedDirectory);

        Measure("2 Write every cover sample", samples.Count, i =>
        {
            var name = Path.GetFileNameWithoutExtension(samples[i]) + ".expected.webp";
            _pipeline.Write(new ImageSource.FromFile(samples[i]), ImageKind.ChapterCover, Policy, ExpectedDirectory, name);
        });

        foreach (var sample in samples)
        {
            var expected = Path.Join(ExpectedDirectory, Path.GetFileNameWithoutExtension(sample) + ".expected.webp");
            using var image = Image.NewFromFile(expected, access: Enums.Access.Sequential);
            output.WriteLine($"{Path.GetFileName(sample)} -> {image.Width}x{image.Height}, {new FileInfo(expected).Length / 1024.0:F1} KB");
        }
    }

    [Fact(Skip = Manual)]
    public void Scenario3_ReencodeFiftyLargeCovers()
    {
        var covers = CoverSamples().Select(File.ReadAllBytes).ToList();
        Measure("3 Reencode 50 large covers", 50, i =>
            _pipeline.Reencode(covers[i % covers.Count], ImageKind.ChapterCover, Policy, OutputDirectory, $"s3-{i % covers.Count}_output.webp"));
    }

    [Theory(Skip = Manual)]
    [InlineData(EncodeFormat.WEBP, false)]
    [InlineData(EncodeFormat.WEBP, true)]
    [InlineData(EncodeFormat.JPEG, false)]
    [InlineData(EncodeFormat.JPEG, true)]
    public void Scenario4_KoboCover(EncodeFormat format, bool greyscale)
    {
        var cover = EnsureKoboCover(format);
        Measure($"4 Kobo {format} greyscale={greyscale}", 50, _ => KoboCoverAsOfToday(cover, greyscale));
    }

    /// <summary>
    /// A copy of KoboController.GetBookCover's render as of 2026-11-21, so the new code has something to be compared with.
    /// Today's controller skips this for a .jpg file, the comparison runs it for both
    /// </summary>
    private static byte[] KoboCoverAsOfToday(string fullPath, bool greyscale)
    {
        using var img = Image.Thumbnail(fullPath, KoboWidth, height: KoboHeight, size: Enums.Size.Down);
        using var final = greyscale ? img.Colourspace(Enums.Interpretation.Bw) : img;
        return final.JpegsaveBuffer(q: KoboQuality);
    }

    private void Measure(string scenario, int iterations, Action<int> run)
    {
        Directory.CreateDirectory(OutputDirectory);

        // Warm NetVips' operation cache and the JIT so the measured pass compares like with like
        for (var i = 0; i < iterations; i++) run(i);

        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var collectionsBefore = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < iterations; i++) run(i);

        stopwatch.Stop();
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        var collections = Enumerable.Range(0, 3).Select(g => GC.CollectionCount(g) - collectionsBefore[g]).ToArray();
        using var process = Process.GetCurrentProcess();

        var row = new BaselineRow(scenario, iterations,
            Math.Round(stopwatch.Elapsed.TotalMilliseconds / iterations, 1),
            Math.Round(allocated / 1024.0 / 1024.0, 2),
            collections[0], collections[1], collections[2],
            Math.Round(Stats.MemHighwater / 1024.0 / 1024.0, 1),
            Math.Round(process.PeakWorkingSet64 / 1024.0 / 1024.0, 1));

        output.WriteLine(row.ToString());

        var csv = Path.Join(OutputDirectory, "baseline_output.csv");
        if (!File.Exists(csv))
        {
            File.WriteAllText(csv, "when,scenario,iterations,ms per op,managed MB,gen0,gen1,gen2,vips highwater MB,peak working set MB" + Environment.NewLine);
        }
        File.AppendAllText(csv, $"{DateTime.Now:yyyy-MM-dd h:mm tt},{row.ToCsv()}{Environment.NewLine}");
    }

    private static List<string> CoverSamples()
    {
        return Directory.EnumerateFiles(CoversDirectory)
            .Where(f => !Path.GetFileName(f).Contains("_output") && !Path.GetFileName(f).Contains("_baseline"))
            .Where(f => Path.GetExtension(f) is ".jpg" or ".png")
            .Order()
            .ToList();
    }

    /// <summary>
    /// comic-normal-3.jpg (1988x3056) enlarged 2x, about 4000x6000; made once and gitignored by its _output name
    /// </summary>
    private static string EnsureLargePage()
    {
        if (File.Exists(LargePage)) return LargePage;

        Directory.CreateDirectory(OutputDirectory);
        using var source = Image.NewFromFile(Path.Join(CoversDirectory, "comic-normal-3.jpg"));
        using var large = source.Resize(2);
        large.Jpegsave(LargePage, q: 90);
        return LargePage;
    }

    /// <summary>
    /// A stored cover at the Medium size (640x909), larger than the Kobo request, so the resize does real work
    /// </summary>
    private string EnsureKoboCover(EncodeFormat format)
    {
        var fileName = "kobo-cover_output" + format.GetExtension();
        var path = Path.Join(OutputDirectory, fileName);
        if (File.Exists(path)) return path;

        Directory.CreateDirectory(OutputDirectory);
        _pipeline.Write(new ImageSource.FromFile(Path.Join(CoversDirectory, "comic-normal.jpg")), ImageKind.ChapterCover,
            new ImagePolicy(format, CoverImageSize.Medium), OutputDirectory, fileName);
        return path;
    }

    private sealed record BaselineRow(string Scenario, int Iterations, double MsPerOp, double ManagedMb,
        int Gen0, int Gen1, int Gen2, double VipsHighwaterMb, double PeakWorkingSetMb)
    {
        public string ToCsv() => $"{Scenario},{Iterations},{MsPerOp},{ManagedMb},{Gen0},{Gen1},{Gen2},{VipsHighwaterMb},{PeakWorkingSetMb}";
    }
}
