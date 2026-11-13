using System.Text.Json;
using NetVips;
using Xunit.Abstractions;

namespace Kavita.Services.Tests.Images;

/// <summary>
/// Manual check for how converting a stored image between formats changes it. Not run by default: add [Fact] to
/// <see cref="Compare"/> locally, run it, then open the compare.html it prints
/// </summary>
public class EncodeFormatComparison(ITestOutputHelper output)
{
    private static readonly string OutputDirectory = Path.Join(Path.GetTempPath(), "kavita-encode-compare");

    public void Compare()
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

        Directory.CreateDirectory(OutputDirectory);
        var results = sources.Select(s => Convert(s.Key, s.Value)).ToList();

        var htmlPath = Path.Join(OutputDirectory, "compare.html");
        File.WriteAllText(htmlPath, Html.Replace("/*RESULTS*/", JsonSerializer.Serialize(results)));
        output.WriteLine(htmlPath);
    }

    private SourceResult Convert(string name, string sourcePath)
    {
        var source = Path.Join(OutputDirectory, name + ".webp");
        File.Copy(sourcePath, source, overwrite: true);

        using (var image = Image.NewFromFile(source)) image.WriteToFile(Path.Join(OutputDirectory, name + ".png"));
        using (var image = Image.NewFromFile(Path.Join(OutputDirectory, name + ".png"))) image.WriteToFile(Path.Join(OutputDirectory, name + "-roundtrip.webp"));
        using (var image = Image.NewFromFile(source)) image.Jpegsave(Path.Join(OutputDirectory, name + ".jpg"), background: [255, 255, 255]);

        using var original = Image.NewFromFile(source);
        var variants = new[] { ".webp", ".png", "-roundtrip.webp", ".jpg" }
            .Select(suffix => Measure(original, name + suffix))
            .ToList();

        output.WriteLine($"{name}: {Path.GetFileName(sourcePath)} {original.Width}x{original.Height}");
        foreach (var v in variants)
        {
            output.WriteLine($"  {v.File,-24} {v.SizeKb,7} KB  max diff {v.MaxDiff}  mean diff {v.MeanDiff:0.000}");
        }

        return new SourceResult(name, $"{original.Width}x{original.Height}", variants);
    }

    private static VariantResult Measure(Image original, string file)
    {
        using var converted = Image.NewFromFile(Path.Join(OutputDirectory, file));
        using var difference = (original - converted).Abs();

        return new VariantResult(file, new FileInfo(Path.Join(OutputDirectory, file)).Length / 1024,
            difference.Max(), Math.Round(difference.Avg(), 3));
    }

    private sealed record SourceResult(string Name, string Size, List<VariantResult> Variants);

    private sealed record VariantResult(string File, long SizeKb, double MaxDiff, double MeanDiff);

    private const string Html = """
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Encode Comparison</title>
        <style>
          :root { --bg: #111; --fg: #ddd; --muted: #999; --border: #333; }
          body { background: var(--bg); color: var(--fg); font: 14px system-ui, sans-serif; margin: 16px; }
          .controls { display: flex; gap: 16px; align-items: center; flex-wrap: wrap; margin-bottom: 12px; }
          .grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 8px; }
          .cell { border: 1px solid var(--border); }
          .cell h3 { margin: 0; padding: 6px; font-size: 13px; }
          .cell small { color: var(--muted); display: block; padding: 0 6px 6px; }
          .view { height: 70vh; overflow: hidden; position: relative; }
          .view img { position: absolute; image-rendering: pixelated; transform-origin: 0 0; }
        </style>
        </head>
        <body>
        <div class="controls">
          <label>Image <select id="which"></select></label>
          <label>Zoom <input id="zoom" type="range" min="1" max="12" step="1" value="4"> <span id="zoomLabel"></span></label>
          <span>Drag any panel to pan, all four move together. Diffs are per channel, 0 to 255, against the source</span>
        </div>
        <div class="grid" id="grid"></div>
        <script>
          const results = /*RESULTS*/;
          const titles = { '.webp': 'Source WebP', '.png': 'Converted to PNG', '-roundtrip.webp': 'PNG back to WebP', '.jpg': 'Converted to JPEG' };
          const which = document.getElementById('which');
          const grid = document.getElementById('grid');
          const zoom = document.getElementById('zoom');
          let x = 0, y = 0;

          for (const r of results) {
            which.add(new Option(`${r.Name} (${r.Size})`, r.Name));
          }

          function render() {
            const result = results.find(r => r.Name === which.value);
            grid.innerHTML = '';
            for (const v of result.Variants) {
              const suffix = v.File.substring(result.Name.length);
              const cell = document.createElement('div');
              cell.className = 'cell';
              cell.innerHTML = `<h3>${titles[suffix]}</h3><small>${v.File}, ${v.SizeKb} KB, max diff ${v.MaxDiff}, mean ${v.MeanDiff}</small><div class="view"><img src="${v.File}"></div>`;
              grid.appendChild(cell);
            }
            apply();
          }

          function apply() {
            document.getElementById('zoomLabel').textContent = zoom.value + 'x';
            for (const img of grid.querySelectorAll('img')) {
              img.style.transform = `translate(${x}px, ${y}px) scale(${zoom.value})`;
            }
          }

          let drag = null;
          grid.addEventListener('pointerdown', e => { drag = { sx: e.clientX - x, sy: e.clientY - y }; e.preventDefault(); });
          window.addEventListener('pointermove', e => { if (!drag) return; x = e.clientX - drag.sx; y = e.clientY - drag.sy; apply(); });
          window.addEventListener('pointerup', () => drag = null);
          zoom.addEventListener('input', apply);
          which.addEventListener('change', () => { x = 0; y = 0; render(); });
          render();
        </script>
        </body>
        </html>
        """;
}
