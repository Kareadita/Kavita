using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Kobo;
using Kavita.Common;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Images;
using OfficeIMO.Epub;
using QuickEPUB;

namespace Kavita.Services.Kobo;

public sealed record BuildEpubOptions(List<string> Pages, ImageRender Render, ReadingDirection ReadingDirection, string OutputPath);

public class EpubBuilderService(ICacheService cacheService, ILocalizationService localizationService,
    IUnitOfWork unitOfWork, IEntityNamingService namingService, IDirectoryService directoryService, IImagePipeline imagePipeline) : IEpubBuilderService
{
    public async Task<string> BuildEpubAsync(int userId, int chapterId, string outputPath, CancellationToken ct = default)
    {
        var chapter = await cacheService.Ensure(chapterId, false, ct);
        if (chapter is null)
        {
            throw new KavitaException(await localizationService.TranslateAsync("cache-file-find"));
        }

        var libraryType = await unitOfWork.ChapterRepository.GetLibraryTypeAsync(chapter.Id, ct);
        var chapterDto = await unitOfWork.ChapterRepository.GetChapterDtoAsync(chapterId, userId, ct);
        var volumeDto = await unitOfWork.VolumeRepository.GetVolumeDtoAsync(chapter.VolumeId, userId, ct);
        if (libraryType is null || chapterDto is null || volumeDto is null)
        {
            throw new KavitaException();
        }


        var namingCtx = await LocalizedNamingContext.CreateAsync(namingService, localizationService, userId, libraryType.Value);
        var title = namingCtx.BuildChapterTitle(volumeDto, chapterDto);

        var authors = chapterDto.Writers.FirstOrDefault()?.Name ?? "Kavita"; // Probably don't need this with Office.IMO

        EpubPublication publication = EpubPublication.Create(title, "en");
        publication.Creator = "Kavita";


        var epub = new Epub(namingCtx.BuildChapterTitle(volumeDto, chapterDto), chapterDto.Writers.FirstOrDefault()?.Name ?? "Kavita");
        var openFiles = new List<Stream>();

        try
        {
            var cover = await unitOfWork.ChapterRepository.GetChapterCoverImageAsync(chapterId, ct);
            var path = Path.Join(directoryService.CoverImageDirectory, cover);

            if (chapterDto.HasCoverImage && File.Exists(path))
            {
                var source = new ImageSource.FromFile(path);
                var coverBytes = imagePipeline.Render(source, new ImageRender(EncodeFormat.JPEG, []));

                publication.AddResource("cover", "EPUB/images/cover.png", "image/png", coverBytes);
                publication.SetCoverImage("cover");
            }

            publication.AddStylesheet("style", "EPUB/style.css",
                "body { font-family: sans-serif; } p { line-height: 1.4; }"); // TODO: Put global styles

            var index = 0;
            foreach (var cachedPage in cacheService.GetCachedPages(chapterId))
            {
                ct.ThrowIfCancellationRequested();

                var source = new ImageSource.FromFile(cachedPage);
                var image = imagePipeline.Render(source, new ImageRender(EncodeFormat.JPEG, []));

                // We need to map from Office.IMO 1-based indexing to Kobo's indexing and handle split pages using a - (once I code that up)
                var fileName = $"page_{index++:D4}";
                publication.AddChapter(fileName, $"EPUB/{fileName}.xhtml", $"Page {index}",
                    $"<div style=\"text-align:center\"><img src=\"{fileName}\" style=\"max-width:100%;max-height:100%\"/></div>",
                    ["style"]);
                publication.AddResource(fileName, $"EPUB/images/{fileName}", "image/png", image);
            }

            // Table of Content generation
            publication.SetNavigation([
                new EpubNavigationEntry("Cover", "EPUB/cover.xhtml"),
                // ... Every page
            ]);

        }
        finally
        {
            foreach (var fs in openFiles)
            {
                await fs.DisposeAsync();
            }
        }

        (await publication.SaveAsync(outputPath, cancellationToken: ct)).ThrowIfLoss();

        return outputPath;
    }
}
