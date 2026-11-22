using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Kobo;
using Kavita.Common;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Person;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Person;
using Kavita.Models.Images;
using Nager.ArticleNumber;
using OfficeIMO.Epub;
using QuickEPUB;

namespace Kavita.Services.Kobo;

public sealed record BuildEpubOptions(List<string> Pages, ImageRender Render,
    ReadingDirection ReadingDirection, string OutputPath); // I probably need Metadata and explicit cover

public class EpubBuilderService(ICacheService cacheService, ILocalizationService localizationService,
    IUnitOfWork unitOfWork, IEntityNamingService namingService, IDirectoryService directoryService, IImagePipeline imagePipeline) : IEpubBuilderService
{
    private const string GlobalStyles = """
                                @page { margin: 0; }
                                html, body { margin: 0; padding: 0; width: 100%; height: 100%; overflow: hidden; }
                                .page { width: 100%; height: 100%; }
                                .page img { display: block; width: 100%; height: 100%; }
                                """;
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


        var language = string.IsNullOrEmpty(chapterDto.Language) ? "en" : chapterDto.Language;
        var publication = EpubPublication.Create(title, language);
        publication.SetRenditionLayout(EpubRenditionLayout.PrePaginated);
        AddMetadata(publication, chapterDto);

        publication.AddStylesheet("style", "EPUB/style.css", GlobalStyles);
        const string mediaOutput = "image/jpeg";

        var cover = await unitOfWork.ChapterRepository.GetChapterCoverImageAsync(chapterId, ct);
        var path = Path.Join(directoryService.CoverImageDirectory, cover);

        if (chapterDto.HasCoverImage && File.Exists(path))
        {
            var source = new ImageSource.FromFile(path);
            var coverBytes = imagePipeline.Render(source, new ImageRender(EncodeFormat.JPEG, []));

            publication.AddResource("cover", "EPUB/images/cover.jpeg", mediaOutput, coverBytes.Data);
            publication.SetCoverImage("cover");
        }



        const string PageChapterHtml = """<div class="page"><img src="images/{{fileName}}" alt="" width="{{width}}" height="{{height}}"/></div>""";



        var index = 1;
        foreach (var cachedPage in cacheService.GetCachedPages(chapterId))
        {
            ct.ThrowIfCancellationRequested();

            var source = new ImageSource.FromFile(cachedPage);
            var image = imagePipeline.Render(source, new ImageRender(EncodeFormat.JPEG, []));

            // We need to map from Office.IMO 1-based indexing to Kobo's indexing and handle split pages using a - (once I code that up)
            var fileName = $"page_{index:D4}";
            var imageId = fileName + "-img";

            var htmlPage = PageChapterHtml.Replace("{{fileName}}", fileName).Replace("{{width}}", $"{image.Width}").Replace("{{height}}", $"{image.Height}");


            publication.AddChapter(fileName, $"EPUB/{fileName}.xhtml", $"Page {index}",
                htmlPage, ["style"]);
            publication.AddResource(imageId, $"EPUB/images/{imageId}.jpeg", mediaOutput, image.Data);
            publication.SetFixedLayoutPage(fileName, new EpubFixedLayoutPage(image.Width, image.Height)
            {
                Spread = EpubPageSpread.None,
            }, ct);
            index++;
        }

        // Table of Content generation
        // publication.SetNavigation([
        //     new EpubNavigationEntry("Cover", "EPUB/cover.xhtml"),
        //     // ... Every page
        // ]);
        (await publication.SaveAsync(outputPath, cancellationToken: ct)).ThrowIfLoss();

        return outputPath;


    }

    private static void AddMetadata(EpubPublication publication, ChapterDto chapterDto)
    {
        AddPersonAttribution(publication, chapterDto.Writers, PersonRole.Writer, "aut", isCreator: true);
        AddPersonAttribution(publication, chapterDto.Pencillers, PersonRole.Penciller, "art", isCreator: true);
        AddPersonAttribution(publication, chapterDto.Inkers, PersonRole.Inker, "art", isCreator: false);
        AddPersonAttribution(publication, chapterDto.Colorists, PersonRole.Colorist, "clr", isCreator: false);
        AddPersonAttribution(publication, chapterDto.Letterers, PersonRole.Letterer, "ill", isCreator: false);
        AddPersonAttribution(publication, chapterDto.CoverArtists, PersonRole.CoverArtist, "cov", isCreator: false);
        AddPersonAttribution(publication, chapterDto.Editors, PersonRole.Editor, "edt", isCreator: false);
        AddPersonAttribution(publication, chapterDto.Translators, PersonRole.Translator, "trl", isCreator: false);

        publication.SetPublicationDetails(new EpubPublicationDetails()
        {
            Description = chapterDto.Summary, // TODO: AnnotationService.StripHtml(chapterDto.Summary) - Need to refactor that elsewhere to avoid html encoding into summary
            PublicationDate = chapterDto.ReleaseDate == DateTime.MinValue ? null : chapterDto.ReleaseDate,
            Publisher = chapterDto.Publishers.FirstOrDefault()?.Name,
        });

        var isbn = chapterDto.ISBN;
        if (!string.IsNullOrEmpty(isbn))
        {
            var isIsbn10 = ArticleNumberHelper.IsValidIsbn10(isbn);
            var isIsbn13 = ArticleNumberHelper.IsValidIsbn13(isbn);

            EpubIdentifierKind kind = EpubIdentifierKind.Unspecified;
            if (isIsbn10) kind = EpubIdentifierKind.Isbn10;
            else if (isIsbn13) kind = EpubIdentifierKind.Isbn13;

            publication.AddIdentifier("isbn", new EpubIdentifierMetadata()
            {
                Value = isbn,
                Kind = kind
            });
        }

        // Genres/Tags -> Subject
        var allTags = chapterDto.Genres.Select(g => g.Title).Concat(chapterDto.Tags.Select(t => t.Title)).Distinct(StringComparer.OrdinalIgnoreCase);
        var index = 1;
        foreach (var tag in allTags)
        {
            publication.AddSubject($"tag-{index}", new EpubSubjectMetadata()
            {
                Text = tag
            });
            index++;
        }

    }

    private static void AddPersonAttribution(EpubPublication publication, ICollection<PersonDto> people,
        PersonRole role, string marcRole, bool isCreator)
    {
        var index = 1;
        foreach (var person in people)
        {
            var id = $"{role}-{index}";
            var metadata = new EpubContributorMetadata
            {
                Name = person.Name,
                MarcRoles = [marcRole],
            };

            if (isCreator)
            {
                publication.AddCreator(id, metadata);
            }
            else
            {
                publication.AddContributor(id, metadata);
            }

            index++;
        }
    }
}
