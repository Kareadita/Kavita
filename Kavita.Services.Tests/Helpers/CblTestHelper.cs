using System.Text.Json;
using System.Xml.Serialization;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.ReadingLists;
using Kavita.API.Services.SignalR;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.ReadingLists.CBL.Internal;
using Kavita.Models.DTOs.ReadingLists.CBL.V1;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;
using Kavita.Services.Builders;
using Kavita.Services.ReadingLists;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Kavita.Services.Tests.Helpers;

#region Seed Models

internal record SeedLibrary(string LibraryName, string LibraryType, List<SeedSeries> Series);
internal record SeedSeries(string Name, string? LocalizedName, string? AgeRating, List<SeedVolume> Volumes);
internal record SeedVolume(string Number, List<JsonElement> Chapters);
internal record SeedChapter(string Number, string? ComicVineId, long? MetronId);

#endregion

public record SeedResult(
    Library Library,
    AppUser User,
    Dictionary<(string Series, string Volume, string Chapter), (int SeriesId, int VolumeId, int ChapterId)> Lookup
);

public class CblTestHelper : IDisposable
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly List<string> _tempFiles = [];

    private static readonly string TestDataDir = Path.Join(
        Directory.GetCurrentDirectory(), "../../../Test Data/CblImportService");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CblTestHelper(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<SeedResult> SeedLibrary(string testCaseFile, string? username = null)
    {
        var json = await File.ReadAllTextAsync(Path.Join(TestDataDir, testCaseFile));
        var seed = JsonSerializer.Deserialize<SeedLibrary>(json, JsonOptions)!;

        var libraryType = Enum.Parse<LibraryType>(seed.LibraryType);
        var library = new LibraryBuilder(seed.LibraryName, libraryType)
            .WithFolderPath(new FolderPathBuilder("/data/" + seed.LibraryName.ToLower()).Build())
            .Build();

        foreach (var seedSeries in seed.Series)
        {
            var seriesMetadataBuilder = new SeriesMetadataBuilder();
            if (!string.IsNullOrEmpty(seedSeries.AgeRating))
            {
                seriesMetadataBuilder.WithAgeRating(Enum.Parse<AgeRating>(seedSeries.AgeRating));
            }

            var seriesBuilder = new SeriesBuilder(seedSeries.Name)
                .WithMetadata(seriesMetadataBuilder.Build());

            if (!string.IsNullOrEmpty(seedSeries.LocalizedName))
            {
                seriesBuilder.WithLocalizedName(seedSeries.LocalizedName);
            }

            foreach (var seedVolume in seedSeries.Volumes)
            {
                var volumeBuilder = new VolumeBuilder(seedVolume.Number);

                foreach (var chapterElement in seedVolume.Chapters)
                {
                    var seedChapter = ParseChapterElement(chapterElement);
                    var chapter = new ChapterBuilder(seedChapter.Number).Build();

                    if (!string.IsNullOrEmpty(seedChapter.ComicVineId))
                    {
                        chapter.ComicVineId = seedChapter.ComicVineId;
                    }
                    if (seedChapter.MetronId.HasValue)
                    {
                        chapter.MetronId = seedChapter.MetronId.Value;
                    }

                    volumeBuilder.WithChapter(chapter);
                }

                seriesBuilder.WithVolume(volumeBuilder.Build());
            }

            library.Series ??= [];
            library.Series.Add(seriesBuilder.Build());
        }

        var user = new AppUserBuilder(username ?? "testuser", $"{username ?? "testuser"}@test.com")
            .WithLibrary(library)
            .Build();

        _unitOfWork.UserRepository.Add(user);
        await _unitOfWork.CommitAsync();

        // Build lookup map
        var lookup = new Dictionary<(string, string, string), (int, int, int)>();
        foreach (var series in library.Series!)
        {
            foreach (var volume in series.Volumes)
            {
                foreach (var chapter in volume.Chapters)
                {
                    // Use the range (which matches the input number) for lookup
                    lookup[(series.Name, volume.Name, chapter.Range)] = (series.Id, volume.Id, chapter.Id);
                }
            }
        }

        return new SeedResult(library, user, lookup);
    }

    public async Task<AppUser> AddUser(string username, Library library, AgeRating? restriction = null, bool includeUnknowns = false)
    {
        var user = new AppUserBuilder(username, $"{username}@test.com")
            .WithLibrary(library)
            .Build();

        if (restriction.HasValue)
        {
            user.AgeRestriction = restriction.Value;
            user.AgeRestrictionIncludeUnknowns = includeUnknowns;
        }

        _unitOfWork.UserRepository.Add(user);
        await _unitOfWork.CommitAsync();
        return user;
    }

    public CblImportService CreateImportService()
    {
        return new CblImportService(
            _unitOfWork,
            Substitute.For<ICblGithubService>(),
            Substitute.For<IEventHub>(),
            Substitute.For<IDirectoryService>(),
            Substitute.For<IReadingListService>(),
            Substitute.For<IUrlValidationService>(),
            Substitute.For<IImageService>(),
            Substitute.For<ILogger<CblImportService>>()
        );
    }

    // Overload so we can mock some of the services
    public CblImportService CreateSyncImportService(
        ICblGithubService githubService,
        IDirectoryService directoryService,
        IReadingListService readingListService)
    {
        return new CblImportService(
            _unitOfWork,
            githubService,
            Substitute.For<IEventHub>(),
            directoryService,
            readingListService,
            Substitute.For<IUrlValidationService>(),
            Substitute.For<IImageService>(),
            Substitute.For<ILogger<CblImportService>>()
        );
    }

    public string WriteCblToDisk(ParsedCblReadingList cbl)
    {
        var tempPath = Path.Join(Path.GetTempPath(), $"kavita-test-{Guid.NewGuid()}.cbl");
        _tempFiles.Add(tempPath);

        var serializer = new XmlSerializer(typeof(CblReadingList));
        using var stream = File.Create(tempPath);
        serializer.Serialize(stream, ToCblV1(cbl));

        return tempPath;
    }

    public static string SerializeCblToXml(ParsedCblReadingList cbl)
    {
        var serializer = new XmlSerializer(typeof(CblReadingList));
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
        serializer.Serialize(writer, ToCblV1(cbl));
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static CblReadingList ToCblV1(ParsedCblReadingList cbl)
    {
        return new CblReadingList
        {
            Name = cbl.Name,
            Summary = cbl.Summary,
            StartYear = cbl.StartYear,
            StartMonth = cbl.StartMonth,
            EndYear = cbl.EndYear,
            EndMonth = cbl.EndMonth,
            Books = new CblBooks
            {
                Book = cbl.Items.Select(item =>
                {
                    var book = new CblBook
                    {
                        Series = item.SeriesName,
                        Number = item.Number,
                        Volume = item.Volume,
                        Year = item.Year,
                    };

                    book.Databases = item.ExternalIds.Select(extId => new CblBookDatabase
                    {
                        Name = extId.Provider switch
                        {
                            CblExternalDbProvider.ComicVine => "cv",
                            CblExternalDbProvider.Metron => "metron",
                            CblExternalDbProvider.GrandComicsDatabase => "gcd",
                            _ => "unknown"
                        },
                        Series = extId.SeriesId,
                        Issue = extId.IssueId
                    }).ToList();

                    return book;
                }).ToList()
            }
        };
    }

    private static SeedChapter ParseChapterElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return new SeedChapter(element.GetString()!, null, null);
        }

        var number = element.GetProperty("number").GetString()!;
        string? comicVineId = null;
        long? metronId = null;

        if (element.TryGetProperty("comicVineId", out var cvProp))
        {
            comicVineId = cvProp.GetString();
        }
        if (element.TryGetProperty("metronId", out var metronProp))
        {
            metronId = metronProp.GetInt64();
        }

        return new SeedChapter(number, comicVineId, metronId);
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); } catch { /* best effort */ }
        }
    }
}
