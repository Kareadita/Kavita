using System.Text.Json;
using Kavita.Services.Scanner;
using NetVips;

namespace Kavita.Services.Tests.Images;

/// <summary>
/// Shared parts of the visual image suites: samples from Test Data/ImageService, _output files written next to a
/// gallery page, and one HTML template for every suite
/// </summary>
public abstract class AbstractImageTest
{
    protected static readonly string ImageServiceDirectory =
        Path.GetFullPath(Path.Join(Directory.GetCurrentDirectory(), "../../../Test Data/ImageService"));
    protected static readonly string CoversDirectory = Path.Join(ImageServiceDirectory, "Covers");
    protected static readonly string TransparencyDirectory = Path.Join(ImageServiceDirectory, "Transparency");

    private const string OutputSuffix = "_output";

    /// <summary>
    /// Where this suite writes its _output files and gallery page
    /// </summary>
    protected abstract string OutputDirectory { get; }

    /// <summary>
    /// Source images in a folder, without generated outputs
    /// </summary>
    protected static IEnumerable<string> GetSamples(string directory)
    {
        return Directory.EnumerateFiles(directory)
            .Where(f => !Path.GetFileName(f).Contains(OutputSuffix))
            .Where(Parser.IsImage)
            .Order();
    }

    protected void ClearOutputs()
    {
        Directory.CreateDirectory(OutputDirectory);
        foreach (var file in Directory.GetFiles(OutputDirectory, "*" + OutputSuffix + ".*"))
        {
            File.Delete(file);
        }
    }

    /// <summary>
    /// The sample's extension stays in the name: transparent.png and transparent.webp would otherwise share outputs
    /// </summary>
    protected string OutputPath(string sample, string caseName, string extension)
    {
        var stem = Path.GetFileName(sample).Replace('.', '-');
        return Path.Join(OutputDirectory, $"{stem}-{caseName}{OutputSuffix}{extension}");
    }

    protected static ImageInfo Measure(string path)
    {
        using var image = Image.NewFromFile(path);
        return ImageInfo.From(image, new FileInfo(path).Length);
    }

    protected static ImageInfo Measure(byte[] bytes)
    {
        using var image = Image.NewFromBuffer(bytes);
        return ImageInfo.From(image, bytes.Length);
    }

    /// <summary>
    /// A cell for a file on disk; the page links it relative to <see cref="OutputDirectory"/>, so sources are not copied
    /// </summary>
    protected GalleryCell Cell(string title, string path, string? note = null, int? boxWidth = null, int? boxHeight = null)
    {
        var info = Measure(path);
        var caption = note == null ? info.ToString() : $"{info}, {note}";
        return new GalleryCell(title, Path.GetRelativePath(OutputDirectory, path).Replace('\\', '/'), caption, boxWidth, boxHeight);
    }

    /// <summary>
    /// Writes the gallery page and returns its path
    /// </summary>
    /// <param name="script">Extra JavaScript run after the page renders, for a suite's own controls</param>
    protected string WriteGallery(string fileName, string title, IReadOnlyList<GalleryRow> rows, string? script = null)
    {
        Directory.CreateDirectory(OutputDirectory);
        var path = Path.Join(OutputDirectory, fileName);
        var html = GalleryHtml
            .Replace("{{TITLE}}", System.Net.WebUtility.HtmlEncode(title))
            .Replace("{{ROWS}}", JsonSerializer.Serialize(rows))
            .Replace("{{SCRIPT}}", script ?? string.Empty);
        File.WriteAllText(path, html);
        return path;
    }

    protected sealed record GalleryRow(string Sample, IReadOnlyList<GalleryCell> Cells);

    protected sealed record GalleryCell(string Title, string File, string Caption, int? BoxWidth = null, int? BoxHeight = null);

    protected sealed record ImageInfo(int Width, int Height, int Bands, bool HasAlpha, string Loader, long Bytes)
    {
        public static ImageInfo From(Image image, long bytes)
        {
            var loader = image.Contains("vips-loader") ? (string) image.Get("vips-loader") : "unknown";
            return new ImageInfo(image.Width, image.Height, image.Bands, image.HasAlpha(), loader, bytes);
        }

        public override string ToString()
        {
            var format = Loader.Replace("load_buffer", string.Empty).Replace("load", string.Empty);
            return $"{format} {Width}x{Height}, {Bands} band{(Bands == 1 ? "" : "s")}{(HasAlpha ? " (alpha)" : "")}, {Bytes / 1024.0:0.#} KB";
        }
    }

    private const string GalleryHtml = """
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>{{TITLE}}</title>
        <style>
          :root {
            --page-bg: #161616; --fg: #ddd; --muted: #999; --border: #333; --box: #ff2d95;
            --frame-bg: repeating-conic-gradient(#bbb 0% 25%, #fff 0% 50%) 0 0 / 1rem 1rem;
          }
          body { background: var(--page-bg); color: var(--fg); font: 0.875rem system-ui, sans-serif; margin: 1rem; }
          h1 { font-size: 1.125rem; margin: 0 0 0.75rem; }
          .controls { display: flex; gap: 1rem; align-items: center; flex-wrap: wrap; margin-bottom: 1rem;
            position: sticky; top: 0; background: var(--page-bg); padding: 0.5rem 0; z-index: 1; }
          .row { border-top: 1px solid var(--border); padding: 0.75rem 0; }
          .row h2 { font-size: 0.9375rem; margin: 0 0 0.5rem; }
          .cells { display: flex; gap: 0.75rem; align-items: flex-start; overflow-x: auto; }
          .cell { flex-shrink: 0; }
          .cell h3 { margin: 0 0 0.25rem; font-size: 0.8125rem; }
          .cell small { color: var(--muted); display: block; margin-bottom: 0.25rem; max-width: 20rem; }
          .frame { position: relative; display: inline-block; background: var(--frame-bg); line-height: 0; }
          .frame img { image-rendering: pixelated; }
          .box { position: absolute; left: 0; top: 0; outline: 1px dashed var(--box); pointer-events: none; }
          body.no-box .box { display: none; }
        </style>
        </head>
        <body>
        <h1>{{TITLE}}</h1>
        <div class="controls">
          <label>Background <select id="background">
            <option value="checker">Checker</option>
            <option value="#000">Black</option>
            <option value="#fff">White</option>
            <option value="#8a8a8a">Grey</option>
          </select></label>
          <label>Zoom <select id="zoom">
            <option value="0.25">25%</option><option value="0.5">50%</option><option value="1" selected>100%</option>
            <option value="2">200%</option><option value="4">400%</option><option value="8">800%</option>
          </select></label>
          <label><input id="showBox" type="checkbox" checked> Target box</label>
          <label>Sample <select id="sample"><option value="">All</option></select></label>
        </div>
        <div id="rows"></div>
        <script>
          const rows = {{ROWS}};
          const rowsEl = document.getElementById('rows');
          const zoom = document.getElementById('zoom');
          const sample = document.getElementById('sample');
          const background = document.getElementById('background');
          const checker = getComputedStyle(document.documentElement).getPropertyValue('--frame-bg');

          for (const r of rows) sample.add(new Option(r.Sample, r.Sample));

          function render() {
            rowsEl.innerHTML = '';
            for (const r of rows.filter(r => !sample.value || r.Sample === sample.value)) {
              const row = document.createElement('div');
              row.className = 'row';
              row.innerHTML = `<h2>${r.Sample}</h2><div class="cells"></div>`;
              for (const c of r.Cells) {
                const cell = document.createElement('div');
                cell.className = 'cell';
                const box = c.BoxWidth ? `<div class="box" data-w="${c.BoxWidth}" data-h="${c.BoxHeight}"></div>` : '';
                cell.innerHTML = `<h3>${c.Title}</h3><small>${c.Caption}</small><div class="frame"><img src="${c.File}" alt="${r.Sample} ${c.Title}">${box}</div>`;
                row.querySelector('.cells').appendChild(cell);
              }
              rowsEl.appendChild(row);
            }
            for (const img of rowsEl.querySelectorAll('img')) {
              img.addEventListener('load', applyZoom);
            }
            applyZoom();
          }

          function applyZoom() {
            const z = Number(zoom.value);
            for (const img of rowsEl.querySelectorAll('img')) {
              if (img.naturalWidth) img.style.width = (img.naturalWidth * z) + 'px';
            }
            for (const box of rowsEl.querySelectorAll('.box')) {
              box.style.width = (box.dataset.w * z) + 'px';
              box.style.height = (box.dataset.h * z) + 'px';
            }
          }

          background.addEventListener('change', () => document.documentElement.style.setProperty('--frame-bg',
            background.value === 'checker' ? checker : background.value));
          document.getElementById('showBox').addEventListener('change', e => document.body.classList.toggle('no-box', !e.target.checked));
          zoom.addEventListener('change', applyZoom);
          sample.addEventListener('change', render);
          render();
          {{SCRIPT}}
        </script>
        </body>
        </html>
        """;
}
