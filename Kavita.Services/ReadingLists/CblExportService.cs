using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.ReadingLists;
using Kavita.Models.DTOs.ReadingLists.CBL.V1;
using Kavita.Models.DTOs.ReadingLists.CBL.V2;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.ReadingLists;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.ReadingLists;

public partial class CblExportService(IUnitOfWork unitOfWork, IDirectoryService directoryService, ILogger<CblExportService> logger) : ICblExportService
{
    private static readonly XmlWriterSettings CblV1XmlOptions = new XmlWriterSettings
    {
        Indent = true,
        Encoding = System.Text.Encoding.UTF8,
    };

    private static readonly JsonSerializerOptions CblV2JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <inheritdoc />
    public async Task<string?> ExportReadingList(int readingListId, int userId, bool asV2 = false)
    {
        try
        {
            var readingList = await unitOfWork.DataContext.ReadingList
                .AsNoTracking()
                .FirstOrDefaultAsync(rl => rl.Id == readingListId);

            if (readingList == null) return null;

            var items = await unitOfWork.DataContext.ReadingListItem
                .AsNoTracking()
                .Where(rli => rli.ReadingListId == readingListId)
                .OrderBy(rli => rli.Order)
                .Include(rli => rli.Chapter)
                .Include(rli => rli.Volume)
                .Include(rli => rli.Series)
                .ThenInclude(s => s.Metadata)
                .ThenInclude(m => m.People)
                .ThenInclude(smp => smp.Person)
                .ToListAsync();

            var outputDir = Path.Combine(directoryService.TempDirectory, userId.ToString(), "cbl-export", $"{readingListId}");
            Directory.CreateDirectory(outputDir);

            var sanitizedName = SanitizeFileName(readingList.Title);

            if (asV2)
            {
                var jsonFileName = $"{sanitizedName}.json";
                var jsonFilePath = Path.Combine(outputDir, jsonFileName);

                var v2 = BuildCblV2Root(readingList, items);
                SerializeV2(v2, jsonFilePath);

                return jsonFilePath;
            }

            var cblFileName = $"{sanitizedName}.cbl";
            var cblFilePath = Path.Combine(outputDir, cblFileName);

            var cbl = BuildCblReadingList(readingList, items);
            SerializeV1(cbl, cblFilePath);

            return cblFilePath;
        } catch (Exception e)
        {
            logger.LogError(e, "Error while exporting reading list: {ReadingListId}", readingListId);
            return null;
        }
    }

    public static CblReadingList BuildCblReadingList(ReadingList readingList, IList<ReadingListItem> items)
    {
        var books = new List<CblBook>();

        foreach (var item in items)
        {
            var year = item.Chapter.ReleaseDate != DateTime.MinValue
                ? item.Chapter.ReleaseDate.Year.ToString()
                : string.Empty;

            var seriesName = GetSeriesAndYearFromName(item, ref year);

            books.Add(new CblBook
            {
                Series = seriesName,
                Number = item.Chapter.Range, // Range can leak internal encodings. Need to understand how to map this.
                Volume = item.Volume.Name, // NOTE: If the library is Comic type, we can try and parse from Kavita Series first. Need to test with real user files
                Year = year,
                Format = (item.Series.Name.Contains("Annual") || item.Chapter.Range.Contains("Annual")) ? "Annual" : string.Empty, // We will only write "Annual" when we detect it in the Series Name
                FileType = MapMangaFormatToFileType(item.Series.Format),
                Databases = GetV1Databases(item.Chapter, item.Series),
            });
        }

        return new CblReadingList
        {
            Name = readingList.Title,
            Summary = readingList.Summary ?? string.Empty,
            StartYear = readingList.StartingYear,
            StartMonth = readingList.StartingMonth,
            EndYear = readingList.EndingYear,
            EndMonth = readingList.EndingMonth,
            Books = new CblBooks { Book = books },
        };
    }

    private static string GetSeriesAndYearFromName(ReadingListItem item, ref string year)
    {
        var seriesName = item.Series.Name;
        var group = SeriesAndYearRegex().Matches(item.Series.Name);
        if (group.Count > 1)
        {
            seriesName = group[0].Groups["Series"].Value.Trim();
            year = group[0].Groups["Year"].Value.Trim();
        }

        return seriesName;
    }

    public static void SerializeV1(CblReadingList cbl, string filePath)
    {
        var serializer = new XmlSerializer(typeof(CblReadingList));

        using var stream = File.Create(filePath);
        using var writer = XmlWriter.Create(stream, CblV1XmlOptions);
        serializer.Serialize(writer, cbl);
    }

    public static CblV2Root BuildCblV2Root(ReadingList readingList, IList<ReadingListItem> items)
    {
        var publisher = GetMostCommonPerson(items, PersonRole.Publisher);
        var imprint = GetMostCommonPerson(items, PersonRole.Imprint);

        var issues = new List<CblV2Issue>();
        foreach (var item in items)
        {
            var year = string.Empty;
            var seriesName = GetSeriesAndYearFromName(item, ref year);

            var coverDate = item.Chapter.ReleaseDate != DateTime.MinValue
                ? item.Chapter.ReleaseDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : string.Empty;

            var seriesStartYear = item.Series.Metadata?.ReleaseYear is > 0
                ? item.Series.Metadata.ReleaseYear
                : int.TryParse(year, out var parsedYear) ? parsedYear : (int?)null;


            issues.Add(new CblV2Issue
            {
                SeriesName = seriesName,
                SeriesStartYear = seriesStartYear,
                IssueNumber = item.Chapter.Range,
                IssueCoverDate = coverDate,
                IssueType = string.Empty,
                Id = GetExternalIds(item.Chapter, item.Series)
            });
        }

        return new CblV2Root
        {
            FileDetails = new CblV2FileDetails
            {
                UUID = Guid.NewGuid().ToString(),
                Version = 1.0,
            },
            ListDetails = new CblV2ListDetails
            {
                Name = readingList.Title,
                Description = readingList.Summary ?? string.Empty,
                StartYear = readingList.StartingYear > 0 ? readingList.StartingYear : null,
                EndYear = readingList.EndingYear > 0 ? readingList.EndingYear : null,
                Publisher = publisher ?? string.Empty,
                Imprint = imprint ?? string.Empty,
                Type = string.Empty,
                Tags = [],
                CoverImageURLs = [],
                Relationships = [],
                Source = [],
            },
            IssueList = issues,
            Notes = string.Empty,
        };
    }

    private static List<CblBookDatabase> GetV1Databases(Chapter chapter, Series series)
    {
        var results = new List<CblBookDatabase>();

        if (!string.IsNullOrEmpty(chapter.ComicVineId))
        {
            if (!string.IsNullOrEmpty(series.ComicVineId))
            {
                results.Add(new CblBookDatabase { Name = "cv", Series = series.ComicVineId, Issue = chapter.ComicVineId });
            }
            else
            {
                results.Add(new CblBookDatabase { Name = "cv", Issue = chapter.ComicVineId });
            }
        }


        if (chapter.MetronId > 0)
            results.Add(new CblBookDatabase { Name = "metron", Issue = chapter.MetronId.ToString() });

        if (chapter.AniListId > 0)
            results.Add(new CblBookDatabase { Name = "anilist", Series = chapter.AniListId.ToString(), Issue = chapter.AniListId.ToString() });

        if (chapter.MalId > 0)
            results.Add(new CblBookDatabase { Name = "malist", Series = chapter.MalId.ToString(), Issue = chapter.MalId.ToString() });

        if (chapter.HardcoverId > 0)
            results.Add(new CblBookDatabase { Name = "hardcover", Issue = chapter.HardcoverId.ToString() });

        return results;
    }

    private static List<CblV2ExternalId> GetExternalIds(Chapter chapter, Series series)
    {
        var results = new List<CblV2ExternalId>();
        if (chapter.AniListId > 0)
        {
            results.Add(new CblV2ExternalId()
            {
                Issue = chapter.AniListId.ToString(),
                Name = "anilist",
                Series = chapter.AniListId.ToString()
            });
        }

        if (chapter.MalId > 0)
        {
            results.Add(new CblV2ExternalId()
            {
                Issue = chapter.MalId.ToString(),
                Name = "malist",
                Series = chapter.MalId.ToString()
            });
        }

        if (!string.IsNullOrEmpty(chapter.ComicVineId))
        {
            results.Add(new CblV2ExternalId()
            {
                Issue = chapter.ComicVineId,
                Name = "cv",
                Series = series.ComicVineId
            });
        }

        if (chapter.MetronId > 0)
        {
            results.Add(new CblV2ExternalId()
            {
                Issue = chapter.MetronId.ToString(),
                Name = "metron",
                Series = series.MetronId.ToString()
            });
        }

        if (chapter.HardcoverId > 0)
        {
            results.Add(new CblV2ExternalId()
            {
                Issue = chapter.HardcoverId.ToString(),
                Name = "hardcover",
                Series = series.HardcoverId.ToString()
            });
        }

        return results;
    }

    public static void SerializeV2(CblV2Root root, string filePath)
    {

        var json = JsonSerializer.Serialize(root, CblV2JsonOptions);
        File.WriteAllText(filePath, json);
    }

    public static string MapMangaFormatToFileType(MangaFormat format)
    {
        return format switch
        {
            MangaFormat.Archive => "cbz",
            MangaFormat.Epub => "epub",
            MangaFormat.Pdf => "pdf",
            MangaFormat.Image => "image",
            _ => string.Empty,
        };
    }

    public static string? GetMostCommonPerson(IList<ReadingListItem> items, PersonRole role)
    {
        return items
            .Where(i => i.Series?.Metadata?.People != null)
            .SelectMany(i => i.Series.Metadata.People)
            .Where(p => p.Role == role && p.Person != null)
            .GroupBy(p => p.Person.Name)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }

    [GeneratedRegex(@"(?<Series>.+)\((?<Year>\d{4})\)$")]
    private static partial Regex SeriesAndYearRegex();
}
