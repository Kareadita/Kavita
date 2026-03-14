using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using System.Text.Json;
using System.Xml;
using System.Xml.Serialization;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Models.DTOs.ReadingLists.CBL.V1;
using Kavita.Models.DTOs.ReadingLists.CBL.V2;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.ReadingLists;

public interface ICblExportService
{
    /// <summary>
    /// Exports the reading list to a temp file on disk.
    /// </summary>
    /// <remarks>Will overwrite existing files</remarks>
    /// <param name="readingListId"></param>
    /// <param name="userId"></param>
    /// <param name="asV2">Export as CBLv2 (JSON)</param>
    /// <returns>Full file path of the exported file, or null if reading list not found</returns>
    Task<string?> ExportReadingList(int readingListId, int userId, bool asV2 = false);
}

public class CblExportService(IUnitOfWork unitOfWork, IDirectoryService directoryService, ILogger<CblExportService> logger) : ICblExportService
{
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

            books.Add(new CblBook
            {
                Series = item.Series.Name,
                Number = item.Chapter.Range, // Range can leak internal encodings. Need to understand how to map this.
                Volume = item.Volume.Name, // TODO: If the library is Comic type, we can try and parse from Kavita Series first. Need to test with real user files
                Year = year,
                Format = item.Chapter.IsSpecial ? "Annual" : string.Empty, // TODO: Confirm with CBL Group on how to handle Format
                FileType = MapMangaFormatToFileType(item.Series.Format),
                Database = null, // TODO: If we have ComicVine metadata id in Chapter, populate this
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

    public static void SerializeV1(CblReadingList cbl, string filePath)
    {
        var serializer = new XmlSerializer(typeof(CblReadingList));
        var settings = new XmlWriterSettings
        {
            Indent = true,
            Encoding = System.Text.Encoding.UTF8,
        };

        using var stream = File.Create(filePath);
        using var writer = XmlWriter.Create(stream, settings);
        serializer.Serialize(writer, cbl);
    }

    public static CblV2Root BuildCblV2Root(ReadingList readingList, IList<ReadingListItem> items)
    {
        var publisher = GetMostCommonPerson(items, PersonRole.Publisher);
        var imprint = GetMostCommonPerson(items, PersonRole.Imprint);

        var issues = new List<CblV2Issue>();
        foreach (var item in items)
        {
            var coverDate = item.Chapter.ReleaseDate != DateTime.MinValue
                ? item.Chapter.ReleaseDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : string.Empty;

            var seriesStartYear = item.Series.Metadata?.ReleaseYear is > 0
                ? item.Series.Metadata.ReleaseYear
                : (int?)null;

            issues.Add(new CblV2Issue
            {
                SeriesName = item.Series.Name,
                SeriesStartYear = seriesStartYear,
                IssueNumber = item.Chapter.Range,
                IssueCoverDate = coverDate,
                IssueType = string.Empty,
                Id = null, // TODO: When we expand Chapter-level external metadata, create this
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

    public static void SerializeV2(CblV2Root root, string filePath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        var json = JsonSerializer.Serialize(root, options);
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
}
