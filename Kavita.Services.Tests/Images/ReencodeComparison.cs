using System.Text.Json;
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
/// Manual check for what <see cref="ImagePipeline.Reencode"/> does to real images from the dev config folder. Not run by
/// default: add [Fact] to <see cref="Compare"/> locally, run it, then open the reencode.html (in Test Data)
/// </summary>
public class ReencodeComparison(ITestOutputHelper output)
{
    private static readonly string OutputDirectory =
        Path.GetFullPath(Path.Join(Directory.GetCurrentDirectory(), "../../../Test Data/ImageService/Reencode"));
    private static readonly ImagePolicy Policy = new(EncodeFormat.WEBP, CoverImageSize.Default);

    public void Compare()
    {
        var config = Path.GetFullPath(Path.Join(AppContext.BaseDirectory, "../../../../Kavita.Server/config"));
        var (boxWidth, boxHeight) = Policy.CoverSize.GetDimensions();
        var coverSamples = PickCovers(Path.Join(config, "covers"), [
            new CoverWanted("Large cover", ImageKind.SeriesCover, s => s.Width > boxWidth && s.Width < s.Height),
            new CoverWanted("Cover already in the box", ImageKind.ChapterCover, s => s.Width <= boxWidth && s.Height <= boxHeight),
        ]);

        var samples = coverSamples.Append(Largest("Favicon", ImageKind.Favicon, Path.Join(config, "favicons")))
            .Append(Largest("Publisher", ImageKind.Publisher, Path.Join(config, "images", "publishers")))
            .ToList();

        Directory.CreateDirectory(OutputDirectory);
        foreach (var file in Directory.GetFiles(OutputDirectory, "*_output.*"))
        {
            File.Delete(file);
        }

        var pipeline = new ImagePipeline(Substitute.For<ILogger<ImagePipeline>>());
        var results = samples.OfType<Sample>().Select((s, i) => Reencode(pipeline, s, i)).ToList();

        var htmlPath = Path.Join(OutputDirectory, "reencode.html");
        File.WriteAllText(htmlPath, Html.Replace("/*RESULTS*/", JsonSerializer.Serialize(results)));
        output.WriteLine(htmlPath);
    }

    private SampleResult Reencode(ImagePipeline pipeline, Sample sample, int index)
    {
        var before = $"{index}-before_output{Path.GetExtension(sample.Path)}";
        File.Copy(sample.Path, Path.Join(OutputDirectory, before));

        var after = $"{index}-after_output{Policy.Format.ForKind(sample.Kind).GetExtension()}";
        var written = pipeline.Reencode(Path.Join(OutputDirectory, before), sample.Kind, Policy, OutputDirectory, after);
        var box = ImageProfile.For(sample.Kind).GetBox(Policy);

        var result = new SampleResult(sample.Name, Path.GetFileName(sample.Path), box?.Width, box?.Height,
            Measure(before, written.SourceWidth, written.SourceHeight), Measure(after, written.Width, written.Height));
        output.WriteLine($"{sample.Name}: {result.Source} {result.Before.Width}x{result.Before.Height} ({result.Before.SizeKb} KB) " +
                         $"-> {result.After.Width}x{result.After.Height} ({result.After.SizeKb} KB)");

        return result;
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

    private static ImageVersion Measure(string file, int width, int height)
    {
        return new ImageVersion(file, width, height, Math.Round((double) width / height, 3),
            Math.Round(new FileInfo(Path.Join(OutputDirectory, file)).Length / 1024.0, 1));
    }

    private sealed record Sample(string Name, ImageKind Kind, string Path);

    private sealed record CoverWanted(string Name, ImageKind Kind, Func<ImageBox, bool> Match);

    private sealed record SampleResult(string Name, string Source, int? BoxWidth, int? BoxHeight, ImageVersion Before, ImageVersion After);

    private sealed record ImageVersion(string File, int Width, int Height, double Ratio, double SizeKb);

    private const string Html = """
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Reencode Comparison</title>
        <style>
          :root { --bg: #111; --fg: #ddd; --muted: #999; --border: #333; --box: #e0a030; --check-a: #ccc; --check-b: #fff; }
          body { background: var(--bg); color: var(--fg); font: 14px system-ui, sans-serif; margin: 16px; }
          .controls { display: flex; gap: 16px; align-items: center; flex-wrap: wrap; margin-bottom: 16px; }
          .sample { border-top: 1px solid var(--border); padding: 12px 0; }
          .sample h2 { margin: 0 0 4px; font-size: 16px; }
          .sample > small { color: var(--muted); }
          .pair { display: flex; gap: 24px; flex-wrap: wrap; align-items: flex-start; margin-top: 8px; }
          .pair h3 { margin: 0 0 4px; font-size: 13px; }
          .pair small { color: var(--muted); display: block; margin-bottom: 6px; }
          .frame { position: relative; }
          .frame img { display: block; image-rendering: pixelated;
            background: repeating-conic-gradient(var(--check-a) 0 25%, var(--check-b) 0 50%) 0 0 / 16px 16px; }
          .frame .box { position: absolute; left: 0; top: 0; outline: 1px dashed var(--box); pointer-events: none; }
          .legend { color: var(--box); }
        </style>
        </head>
        <body>
        <div class="controls">
          <label>Zoom <input id="zoom" type="range" min="0.25" max="4" step="0.25" value="1"> <span id="zoomLabel"></span></label>
          <span>Both images are drawn at their real pixel size times the zoom. <span class="legend">Dashed outline</span>: the box the kind must fit in</span>
        </div>
        <div id="samples"></div>
        <script>
          const results = /*RESULTS*/;
          const zoom = document.getElementById('zoom');
          const samples = document.getElementById('samples');

          function panel(title, v, r) {
            const box = r.BoxWidth ? `<div class="box" data-w="${r.BoxWidth}" data-h="${r.BoxHeight}"></div>` : '';
            const frameWidth = Math.max(v.Width, r.BoxWidth || 0);
            const frameHeight = Math.max(v.Height, r.BoxHeight || 0);
            return `<div><h3>${title}</h3><small>${v.Width}x${v.Height}, ratio ${v.Ratio}, ${v.SizeKb} KB</small>` +
              `<div class="frame" data-w="${frameWidth}" data-h="${frameHeight}">` +
              `<img src="${v.File}" data-w="${v.Width}" data-h="${v.Height}" alt="${title}">${box}</div></div>`;
          }

          for (const r of results) {
            const box = r.BoxWidth ? `box ${r.BoxWidth}x${r.BoxHeight}` : 'no box, kept at source size';
            const section = document.createElement('div');
            section.className = 'sample';
            section.innerHTML = `<h2>${r.Name}</h2><small>${r.Source}, ${box}</small>` +
              `<div class="pair">${panel('Before', r.Before, r)}${panel('After Reencode', r.After, r)}</div>`;
            samples.appendChild(section);
          }

          function apply() {
            const z = Number(zoom.value);
            document.getElementById('zoomLabel').textContent = z + 'x';
            for (const el of samples.querySelectorAll('[data-w]')) {
              el.style.width = (el.dataset.w * z) + 'px';
              el.style.height = (el.dataset.h * z) + 'px';
            }
          }

          zoom.addEventListener('input', apply);
          apply();
        </script>
        </body>
        </html>
        """;
}
