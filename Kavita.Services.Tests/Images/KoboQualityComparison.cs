using System.IO.Abstractions;
using System.Text.Json;
using Kavita.API.Services;
using Kavita.Common.Extensions;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Extensions;
using Kavita.Models.Images;
using Kavita.Services.Images;
using Kavita.Services.Images.ImageSteps;
using Kavita.Services.Scanner;
using Microsoft.Extensions.Logging;
using NetVips;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests.Images;

/// <summary>
/// Manual check for the Kobo page encoding: JPEG quality, enlarging small pages or not, and passing pages through.
/// Uses the first archive in Test Data/KoboOptimizationService. Uncomment the [Fact] on <see cref="GenerateGallery"/>,
/// run it, then open the kobo-quality.html it prints. PSNR is measured against a lossless render at the same size,
/// so it only scores the JPEG loss, not the resize
/// </summary>
public class KoboQualityComparison(ITestOutputHelper output) : AbstractImageTest
{
    private const int DeviceWidth = 1264;
    private const int DeviceHeight = 1680;
    private const int FallbackQuality = 80;
    private const int DiffAmplification = 8;
    private static readonly int[] Qualities = [60, 70, 75, 80, 85, 90];

    private static readonly string ArchiveDirectory =
        Path.GetFullPath(Path.Join(Directory.GetCurrentDirectory(), "../../../Test Data/KoboOptimizationService"));

    private readonly ImagePipeline _pipeline = new(Substitute.For<ILogger<ImagePipeline>>());
    private readonly DirectoryService _directoryService = new(Substitute.For<ILogger<DirectoryService>>(), new FileSystem());

    protected override string OutputDirectory { get; } = Path.Join(ImageServiceDirectory, "KoboQuality");

    private string PagesDirectory => Path.Join(OutputDirectory, "pages_output");

    // [Fact]
    public void GenerateGallery()
    {
        var archive = Directory.EnumerateFiles(ArchiveDirectory)
            .Where(f => !Path.GetFileName(f).Contains("_output"))
            .Where(Parser.IsArchive)
            .Order()
            .FirstOrDefault();
        Assert.NotNull(archive);

        ClearOutputs();
        _directoryService.ClearAndDeleteDirectory(PagesDirectory);
        new ArchiveService(Substitute.For<ILogger<ArchiveService>>(), _directoryService, Substitute.For<IMediaErrorService>())
            .ExtractArchive(archive, PagesDirectory);

        try
        {
            var pages = _directoryService.GetFilesWithExtension(PagesDirectory, Parser.ImageFileExtensions)
                .OrderByNatural(Path.GetFileNameWithoutExtension)
                .ToList();
            Assert.NotEmpty(pages);

            var galleryPages = PickGalleryPages(pages);
            var variants = BuildVariants();
            var totals = variants.ToDictionary(v => v.Key, _ => new VariantTotal());
            var rows = new List<GalleryRow>();

            foreach (var page in pages)
            {
                var inGallery = galleryPages.Contains(page);
                var cells = new List<GalleryCell>();
                if (inGallery)
                {
                    var sourceCopy = OutputPath(page, "source", Path.GetExtension(page));
                    File.Copy(page, sourceCopy, overwrite: true);
                    cells.Add(Cell("Source", sourceCopy, "as stored in the archive"));
                }

                foreach (var variant in variants)
                {
                    var result = Encode(page, variant);
                    totals[variant.Key].Add(result);
                    if (!inGallery) continue;

                    var file = OutputPath(page, variant.Key, ".jpg");
                    File.WriteAllBytes(file, result.Data);
                    File.WriteAllBytes(OutputPath(page, variant.Key + "-diff", ".png"), result.Diff);
                    cells.Add(Cell(variant.Label, file, $"PSNR {FormatPsnr(result.Psnr)}{(result.PassedThrough ? ", passed through" : "")}"));
                }

                if (inGallery)
                {
                    rows.Add(new GalleryRow(Path.GetFileName(page), cells));
                }
            }

            var sourceBytes = pages.Sum(p => new FileInfo(p).Length);
            var summary = variants.Select(v => totals[v.Key].ToSummary(v.Label, sourceBytes)).ToList();
            foreach (var line in summary)
            {
                output.WriteLine($"{line.Label}: {line.TotalKb:0} KB ({line.VsSource:0.00}x source), mean PSNR {line.MeanPsnr}, worst {line.WorstPsnr}");
            }

            var script = ZoomerScript
                .Replace("{{SUMMARY}}", JsonSerializer.Serialize(summary))
                .Replace("{{SOURCE}}", JsonSerializer.Serialize($"{Path.GetFileName(archive)}: {pages.Count} pages, {sourceBytes / 1024.0:0} KB of images, cbz {new FileInfo(archive).Length / 1024.0:0} KB"))
                .Replace("{{AMP}}", DiffAmplification.ToString());
            output.WriteLine(WriteGallery("kobo-quality.html", "Kobo page quality", rows, script));
        }
        finally
        {
            _directoryService.ClearAndDeleteDirectory(PagesDirectory);
        }
    }

    /// <summary>
    /// First, last, middle and the widest page (usually a two-page spread)
    /// </summary>
    private static HashSet<string> PickGalleryPages(List<string> pages)
    {
        var widest = pages.MaxBy(p =>
        {
            using var header = Image.NewFromFile(p, access: Enums.Access.Sequential);
            return (double) header.Width / header.Height;
        })!;

        return [pages[0], pages[pages.Count / 2], widest, pages[^1]];
    }

    private static List<Variant> BuildVariants()
    {
        var enlarge = new FitToStep(DeviceWidth, DeviceHeight);
        var shrink = new FitWithinStep(DeviceWidth, DeviceHeight);

        return
        [
            ..Qualities.Select(q => new Variant($"enlarge-q{q}", $"Enlarge q{q}", enlarge, q, false)),
            ..Qualities.Select(q => new Variant($"shrink-q{q}", $"Shrink only q{q}", shrink, q, false)),
            new Variant("passthrough", $"Pass-through, else shrink q{FallbackQuality}", shrink, FallbackQuality, true),
        ];
    }

    private EncodeResult Encode(string page, Variant variant)
    {
        var steps = new List<IImageStep> { variant.Resize, new GreyscaleStep() };
        var reference = _pipeline.Render(new ImageSource.FromFile(page), new ImageRender(EncodeFormat.PNG, steps)).Data;

        if (variant.PassThrough && CanPassThrough(page))
        {
            var original = File.ReadAllBytes(page);
            return Compare(original, reference, passedThrough: true);
        }

        var encoded = _pipeline.Render(new ImageSource.FromFile(page), new ImageRender(EncodeFormat.JPEG, steps, variant.Quality)).Data;
        return Compare(encoded, reference, passedThrough: false);
    }

    /// <summary>
    /// A greyscale JPEG that already fits the screen gains nothing from being decoded and encoded again
    /// </summary>
    private static bool CanPassThrough(string page)
    {
        var info = Measure(page);
        return info.Loader.StartsWith("jpeg") && info.Bands == 1 && info.Width <= DeviceWidth && info.Height <= DeviceHeight;
    }

    private static EncodeResult Compare(byte[] encoded, byte[] reference, bool passedThrough)
    {
        using var a = Image.NewFromBuffer(encoded).ExtractBand(0).Cast(Enums.BandFormat.Float);
        using var b = Image.NewFromBuffer(reference).ExtractBand(0).Cast(Enums.BandFormat.Float);
        using var difference = a - b;
        using var squared = difference * difference;
        var mse = squared.Avg();
        var psnr = mse == 0 ? double.PositiveInfinity : 10 * Math.Log10(255 * 255 / mse);

        using var abs = difference.Abs();
        using var amplified = abs * DiffAmplification;
        using var diff = amplified.Cast(Enums.BandFormat.Uchar);
        return new EncodeResult(encoded, diff.PngsaveBuffer(), psnr, passedThrough);
    }

    private static string FormatPsnr(double psnr) => double.IsPositiveInfinity(psnr) ? "lossless" : $"{psnr:0.0} dB";

    private sealed record Variant(string Key, string Label, IResizeStep Resize, int Quality, bool PassThrough);

    private sealed record EncodeResult(byte[] Data, byte[] Diff, double Psnr, bool PassedThrough);

    private sealed record SummaryLine(string Label, double TotalKb, double VsSource, string MeanPsnr, string WorstPsnr, int PassedThrough);

    private sealed class VariantTotal
    {
        private long _bytes;
        private int _passedThrough;
        private readonly List<double> _psnr = [];

        public void Add(EncodeResult result)
        {
            _bytes += result.Data.Length;
            _passedThrough += result.PassedThrough ? 1 : 0;
            _psnr.Add(result.Psnr);
        }

        public SummaryLine ToSummary(string label, long sourceBytes)
        {
            var finite = _psnr.Where(double.IsFinite).ToList();
            var mean = finite.Count == 0 ? double.PositiveInfinity : finite.Average();
            var worst = finite.Count == 0 ? double.PositiveInfinity : finite.Min();
            return new SummaryLine(label, _bytes / 1024.0, (double) _bytes / sourceBytes, FormatPsnr(mean), FormatPsnr(worst), _passedThrough);
        }
    }

    private const string ZoomerScript = """
        const summary = {{SUMMARY}};
        const amp = {{AMP}};

        const style = document.createElement('style');
        style.textContent = `
          .summary { border-collapse: collapse; margin-bottom: 1rem; }
          .summary th, .summary td { border: 1px solid var(--border); padding: 0.25rem 0.5rem; text-align: right; }
          .summary th:first-child, .summary td:first-child { text-align: left; }
          .loupe { position: fixed; left: 0; right: 0; bottom: 0; display: none; flex-wrap: wrap; gap: 0.5rem; padding: 0.5rem;
            background: var(--page-bg); border-top: 1px solid var(--border); z-index: 2; max-height: 60vh; overflow-y: auto; }
          .loupe.open { display: flex; }
          .loupe figure { margin: 0; }
          .loupe figcaption { font-size: 0.75rem; color: var(--muted); }
          .loupe canvas { display: block; image-rendering: pixelated; background: #000; }
          .frame img { cursor: crosshair; }
        `;
        document.head.appendChild(style);

        const source = document.createElement('p');
        source.textContent = {{SOURCE}};
        const table = document.createElement('table');
        table.className = 'summary';
        table.innerHTML = '<tr><th>Variant</th><th>Total KB</th><th>vs source</th><th>Mean PSNR</th><th>Worst PSNR</th><th>Passed through</th></tr>' +
          summary.map(s => `<tr><td>${s.Label}</td><td>${s.TotalKb.toFixed(0)}</td><td>${s.VsSource.toFixed(2)}x</td><td>${s.MeanPsnr}</td><td>${s.WorstPsnr}</td><td>${s.PassedThrough}</td></tr>`).join('');
        rowsEl.before(source, table);

        const controls = document.querySelector('.controls');
        controls.insertAdjacentHTML('beforeend', `
          <label><input id="showDiff" type="checkbox"> Difference x${amp}</label>
          <label><input id="showLoupe" type="checkbox" checked> Zoomer</label>
          <label>Zoomer <select id="loupeMag"><option>2</option><option selected>4</option><option>8</option><option>16</option></select>x</label>
          <label>Size <select id="loupeSize"><option>128</option><option selected>192</option><option>256</option></select>px</label>`);

        const showDiff = document.getElementById('showDiff');
        function applyDiff() {
          for (const img of rowsEl.querySelectorAll('img')) {
            if (!img.dataset.src) img.dataset.src = img.getAttribute('src');
            const isSource = img.closest('.cell').querySelector('h3').textContent === 'Source';
            img.src = showDiff.checked && !isSource ? img.dataset.src.replace(/_output\.jpg$/, '-diff_output.png') : img.dataset.src;
          }
        }
        showDiff.addEventListener('change', applyDiff);
        sample.addEventListener('change', applyDiff);

        const loupe = document.createElement('div');
        loupe.className = 'loupe';
        document.body.appendChild(loupe);
        let loupeRow = null;

        // Every cell shows the same spot of the page, as a share of its width, so an enlarged and a shrink-only page line up
        rowsEl.addEventListener('mousemove', e => {
          if (!document.getElementById('showLoupe').checked || e.target.tagName !== 'IMG') return;
          const row = e.target.closest('.row');
          const imgs = [...row.querySelectorAll('img')];
          const size = Number(document.getElementById('loupeSize').value);
          if (loupeRow !== row || loupe.querySelectorAll('canvas').length !== imgs.length || loupe.querySelector('canvas').width !== size) {
            loupeRow = row;
            loupe.innerHTML = imgs.map(i => `<figure><figcaption>${i.closest('.cell').querySelector('h3').textContent}</figcaption><canvas width="${size}" height="${size}"></canvas></figure>`).join('');
          }
          loupe.classList.add('open');

          const box = e.target.getBoundingClientRect();
          const fx = (e.clientX - box.left) / box.width;
          const fy = (e.clientY - box.top) / box.height;
          const widest = Math.max(...imgs.map(i => i.naturalWidth));
          const share = size / Number(document.getElementById('loupeMag').value) / widest;

          loupe.querySelectorAll('canvas').forEach((canvas, n) => {
            const img = imgs[n];
            const span = share * img.naturalWidth;
            const ctx = canvas.getContext('2d');
            ctx.imageSmoothingEnabled = false;
            ctx.fillStyle = '#000';
            ctx.fillRect(0, 0, size, size);
            ctx.drawImage(img, fx * img.naturalWidth - span / 2, fy * img.naturalHeight - span / 2, span, span, 0, 0, size, size);
          });
        });
        rowsEl.addEventListener('mouseleave', () => loupe.classList.remove('open'));

        zoom.value = '0.25';
        applyZoom();
        """;
}
