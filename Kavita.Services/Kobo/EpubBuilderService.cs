using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Kobo;
using Kavita.Common.Extensions;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Person;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Extensions;
using Kavita.Models.Images;
using Kavita.Models.Kobo;
using Kavita.Services.Extensions;
using Kavita.Services.Helpers.Kobo;
using Microsoft.Extensions.Logging;
using Nager.ArticleNumber;
using OfficeIMO.Epub;

namespace Kavita.Services.Kobo;



public class EpubBuilderService(IUnitOfWork unitOfWork, IDirectoryService directoryService, IImagePipeline imagePipeline,
    ILocalizationService localizationService, ILogger<EpubBuilderService> logger) : IEpubBuilderService
{
    private const string GlobalStyles = """
                                @page { margin: 0; }
                                html, body { margin: 0; padding: 0; width: 100%; height: 100%; overflow: hidden; }
                                .page { width: 100%; height: 100%; }
                                .page img { display: block; width: 100%; height: 100%; }
                                """;
    private const string XhtmlMediaType = "application/xhtml+xml";
    private const int MaxParallelRenders = 4;

    public async Task<string> BuildEpubAsync(BuildEpubOptions options, int userId, LocalizedNamingContext naming,
        CancellationToken ct = default)
    {
        logger.LogDebug("Building kepub file");
        var chapterDto = options.Chapter;
        var title = options.BookTitle;
        var language = string.IsNullOrEmpty(chapterDto.Language) ? "en" : chapterDto.Language;
        var extension = options.Render.Format.GetExtension();

        var publication = EpubPublication.Create(title, language);

        SetupEpub(options, publication);
        AddMetadata(publication, chapterDto);
        await AddCoverImage(options, ct, chapterDto, publication, extension);


        var imageMediaType = DownloadService.GetContentTypeFromFile(extension);
        var pageList = new List<EpubNavigationEntry>(options.Pages.Count);

        // Pre-render images in parallel to validate the time savings
        var sw = Stopwatch.StartNew();
        var rendered = RenderPages(options, ct);
        logger.LogDebug("Took {Time}ms to render {Count} pages", sw.ElapsedMilliseconds, rendered.Length);

        for (var i = 0; i < rendered.Length; i++)
        {
            pageList.Add(AddPage(rendered[i], i + 1, extension, imageMediaType, language, publication));
            ct.ThrowIfCancellationRequested();
        }
        logger.LogDebug("Took {Time}ms to add pages", sw.ElapsedMilliseconds);

        var tableOfContents = await BuildTableOfContents(chapterDto, options.Pages.Count, userId, naming);
        publication.SetNavigation(tableOfContents, pageList);

        sw.Restart();
        (await publication.SaveAsync(options.OutputPath, cancellationToken: ct)).ThrowIfLoss();
        logger.LogDebug("Took {Time}ms to write epub", sw.ElapsedMilliseconds);

        return options.OutputPath;
    }

    private async Task<List<EpubNavigationEntry>> BuildTableOfContents(ChapterDto chapter, int pageCount, int userId,
        LocalizedNamingContext naming)
    {
        var groups = KoboEpubHelper.GroupPages(chapter.Files, pageCount, naming.LibraryType);

        var entries = new List<EpubNavigationEntry>(groups.Count);
        foreach (var group in groups)
        {
            var label = group.Kind == EpubTocGroupKind.Chapter
                ? naming.FormatChapterNumber(group.Number!)
                : await localizationService.TranslateAsync(userId, "toc-pages", group.FirstPage + 1, group.LastPage + 1);

            entries.Add(new EpubNavigationEntry(label, KoboEpubHelper.GetPath(group.FirstPage)));
        }

        return entries;
    }

    private ImageRenderResult[] RenderPages(BuildEpubOptions options, CancellationToken ct)
    {
        var rendered = new ImageRenderResult[options.Pages.Count];
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, MaxParallelRenders),
            CancellationToken = ct,
        };

        Parallel.For(0, options.Pages.Count, parallelOptions, i =>
        {
            rendered[i] = imagePipeline.Render(new ImageSource.FromFile(options.Pages[i]), options.Render);
        });

        return rendered;
    }


    private static void SetupEpub(BuildEpubOptions options, EpubPublication publication)
    {
        publication.AddStylesheet("style", "EPUB/style.css", GlobalStyles);
        publication.SetRenditionLayout(EpubRenditionLayout.PrePaginated);
        publication.SetMetadataProperty("rendition:spread", "none");


        if (options.ReadingDirection == ReadingDirection.RightToLeft)
        {
            publication.PageProgressionDirection = "rtl";
        }
    }

    private async Task AddCoverImage(BuildEpubOptions options, CancellationToken ct, ChapterDto chapterDto,
        EpubPublication publication, string extension)
    {
        var cover = await unitOfWork.ChapterRepository.GetChapterCoverImageAsync(chapterDto.Id, ct);
        var path = Path.Join(directoryService.CoverImageDirectory, cover);

        if (chapterDto.HasCoverImage && File.Exists(path))
        {
            var source = new ImageSource.FromFile(path);
            var coverImageResult = imagePipeline.Render(source, options.Render);
            var mediaEncoding = DownloadService.GetContentTypeFromFile(options.Render.Format.GetExtension());

            publication.AddResource("cover", $"EPUB/images/cover{extension}", mediaEncoding, coverImageResult.Data);
            publication.SetCoverImage("cover");
        }
    }

    /// <remarks>
    /// Pages are written as whole XHTML and added as plain resources
    /// </remarks>
    private static EpubNavigationEntry AddPage(ImageRenderResult image, int index, string extension,
        string imageMediaType, string language, EpubPublication publication)
    {
        var pageId = $"page_{index:D4}";
        var imageId = $"image-{index:D4}";
        var imageFile = $"{imageId}{extension}";
        var sectionPath = KoboEpubHelper.GetPath(index - 1);
        var title = $"Page {index}";

        // We can't use SetFixedLayoutMode due to OfficeIMO validating whole package every page, taking minutes (https://github.com/EvotecIT/OfficeIMO/issues/3711)
        publication.AddResource(imageId, $"EPUB/images/{imageFile}", imageMediaType, image.Data);
        publication.AddResource(pageId, sectionPath, XhtmlMediaType,
            Encoding.UTF8.GetBytes(BuildPageXhtml(title, language, imageFile, image.Width, image.Height)));
        publication.AddSpineItem(pageId);

        return new EpubNavigationEntry(index.ToString(CultureInfo.InvariantCulture), sectionPath);
    }


    private static string BuildPageXhtml(string title, string language, string imageFile, int width, int height)
    {
        // No DOCTYPE: OfficeIMO reads content with DtdProcessing.Prohibit and rejects it
        return $"""
                <?xml version="1.0" encoding="utf-8"?>
                <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops" xml:lang="{SecurityElement.Escape(language)}">
                <head>
                <title>{title}</title>
                <link rel="stylesheet" type="text/css" href="style.css"/>
                <meta name="viewport" content="width={width}, height={height}"/>
                </head>
                <body><div class="page"><img src="images/{imageFile}" alt="" width="{width}" height="{height}"/></div></body>
                </html>
                """;
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
            Description = HtmlHelper.ToPlainText(chapterDto.Summary),
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
