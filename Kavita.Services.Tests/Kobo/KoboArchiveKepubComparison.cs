using System.IO.Abstractions;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.Common.Extensions;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Person;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Images;
using Kavita.Models.Kobo;
using Kavita.Services.Helpers;
using Kavita.Services.Images;
using Kavita.Services.Images.ImageSteps;
using Kavita.Services.Kobo;
using Kavita.Services.Scanner;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests.Kobo;

/// <summary>
/// Manual check for whether kepubify adds anything to an epub built from an archive.
/// Drop .cbz/.cbr/.zip files into Test Data/KoboOptimizationService, uncomment the [Fact] on <see cref="GenerateKepubs"/>
/// and run it. Each archive gets a <c>_raw_output.kepub.epub</c> (built epub, only renamed) and a
/// <c>_kepubify_output.kepub.epub</c> (built epub run through the shipped kepubify) to sideload
/// </summary>
public class KoboArchiveKepubComparison(ITestOutputHelper output)
{
    private const ReadingDirection Direction = ReadingDirection.RightToLeft;
    private const string OutputSuffix = "_output";

    private static readonly string TestDirectory =
        Path.GetFullPath(Path.Join(Directory.GetCurrentDirectory(), "../../../Test Data/KoboOptimizationService"));
    private static readonly string PagesDirectory = Path.Join(TestDirectory, $"pages{OutputSuffix}");
    private static readonly string Kepubify =
        Path.GetFullPath(Path.Join(Directory.GetCurrentDirectory(), "../../../../Kavita.Server", KepubifyConverterService.ShippedFileName));

    private readonly DirectoryService _directoryService = new(Substitute.For<ILogger<DirectoryService>>(), new FileSystem());

    // [Fact]
    public async Task GenerateKepubs()
    {
        Directory.CreateDirectory(TestDirectory);
        foreach (var old in Directory.GetFiles(TestDirectory, $"*{OutputSuffix}.*"))
        {
            File.Delete(old);
        }

        var archives = Directory.EnumerateFiles(TestDirectory)
            .Where(f => !Path.GetFileName(f).Contains(OutputSuffix))
            .Where(Parser.IsArchive)
            .Order()
            .ToList();
        Assert.NotEmpty(archives);

        try
        {
            foreach (var archive in archives)
            {
                await Generate(archive);
            }
        }
        finally
        {
            _directoryService.ClearAndDeleteDirectory(PagesDirectory);
        }
    }

    private async Task Generate(string archivePath)
    {
        var name = Path.GetFileNameWithoutExtension(archivePath);
        var pagesDirectory = Path.Join(PagesDirectory, name);
        var builtPath = Path.Join(TestDirectory, $"{name}_built{OutputSuffix}.epub");
        var rawPath = Path.Join(TestDirectory, $"{name}_raw{OutputSuffix}.kepub.epub");
        var kepubifyPath = Path.Join(TestDirectory, $"{name}_kepubify{OutputSuffix}.kepub.epub");

        new ArchiveService(Substitute.For<ILogger<ArchiveService>>(), _directoryService, Substitute.For<IMediaErrorService>())
            .ExtractArchive(archivePath, pagesDirectory);

        var pages = _directoryService.GetFilesWithExtension(pagesDirectory, Parser.ImageFileExtensions)
            .OrderByNatural(Path.GetFileNameWithoutExtension)
            .ToList();

        await BuildEpub(name, archivePath, pages, builtPath);
        await RunKepubify(builtPath, kepubifyPath);
        File.Move(builtPath, rawPath);

        output.WriteLine($"{name}: {pages.Count} pages");
        output.WriteLine($"  {rawPath}");
        output.WriteLine($"  {kepubifyPath}");
    }

    private static async Task BuildEpub(string name, string archivePath, List<string> pages, string outputPath)
    {
        var chapter = new ChapterDto
        {
            Id = 1,
            Pages = pages.Count,
            Language = "en",
            HasCoverImage = true,
            // OfficeIMO's SetPublicationDetails throws "Value cannot be empty (name)" unless both of these are set
            Publishers = [new PersonDto { Name = "Kavita" }],
            Summary = "Kepubify comparison",
            Files = [new MangaFileDto { Id = 1, FilePath = archivePath, Pages = pages.Count, Format = MangaFormat.Archive }],
        };

        // The cover is the first page, as a scan picks it for most archives
        var chapterRepository = Substitute.For<IChapterRepository>();
        chapterRepository.GetChapterCoverImageAsync(chapter.Id, Arg.Any<CancellationToken>()).Returns(Path.GetFileName(pages[0]));
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.ChapterRepository.Returns(chapterRepository);
        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.CoverImageDirectory.Returns(Path.GetDirectoryName(pages[0]));

        var localization = Substitute.For<ILocalizationService>();
        localization.TranslateAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(ci => ci.ArgAt<string>(1) == "toc-pages"
                ? $"Pages {ci.ArgAt<object[]>(2)[0]}-{ci.ArgAt<object[]>(2)[1]}"
                : ci.ArgAt<string>(1));

        var naming = await LocalizedNamingContext.CreateAsync(Substitute.For<IEntityNamingService>(), localization, 1, LibraryType.Manga);
        var builder = new EpubBuilderService(unitOfWork, directoryService, new ImagePipeline(Substitute.For<ILogger<ImagePipeline>>()),
            localization, Substitute.For<ILogger<EpubBuilderService>>());

        await builder.BuildEpubAsync(new BuildEpubOptions(name, pages, LibraRender(), Direction, chapter, outputPath), 1, naming);
    }

    /// <summary>
    /// Matches the default profile in <see cref="KoboOptimizationService"/>
    /// </summary>
    private static ImageRender LibraRender()
    {
        var device = new KoboDeviceProfile { Name = "Libra 2", Width = 1264, Height = 1680, IsGreyscale = true };
        return new ImageRender(EncodeFormat.JPEG, [new FitToStep(device.Width, device.Height), new GreyscaleStep()], 90);
    }

    private static async Task RunKepubify(string epubPath, string kepubPath)
    {
        Assert.True(File.Exists(Kepubify), $"kepubify not found at {Kepubify}");

        var result = await new ProcessCommand.Builder(new ProcessRunner(Substitute.For<ILogger<ProcessRunner>>(), new FileSystem()))
            .WithTimeout(TimeSpan.FromMinutes(5))
            .WithExecutable(Kepubify)
            .AppendArgument(epubPath)
            .AppendArgument("-o")
            .AppendArgument(kepubPath)
            .Build()
            .ExecuteAsync(CancellationToken.None);
        result.ThrowIfFailed();
    }
}
