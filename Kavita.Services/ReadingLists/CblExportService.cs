using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Models.DTOs.ReadingLists.CBL.V1;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Services.ReadingLists;

public interface ICblExportService
{
    /// <summary>
    /// Exports the reading list to a temp file on disk.
    /// </summary>
    /// <param name="readingListId"></param>
    /// <param name="userId"></param>
    /// <param name="asV2">Export as CBLv2 (JSON). Currently not supported.</param>
    /// <returns>Full file path of the exported file, or null if reading list not found</returns>
    Task<string?> ExportReadingList(int readingListId, int userId, bool asV2 = false);
}

public class CblExportService(IUnitOfWork unitOfWork, IDirectoryService directoryService) : ICblExportService
{
    /// <inheritdoc />
    public async Task<string?> ExportReadingList(int readingListId, int userId, bool asV2 = false)
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

        var outputDir = Path.Combine(directoryService.TempDirectory, userId.ToString(), "cbl-export");
        Directory.CreateDirectory(outputDir);

        var sanitizedName = SanitizeFileName(readingList.Title);

        if (asV2)
        {
            throw new NotSupportedException("V2 export is not yet implemented.");
        }

        const string extension = ".cbl";
        var fileName = $"{readingListId}-{sanitizedName}{extension}";
        var filePath = Path.Combine(outputDir, fileName);

        var cbl = BuildCblReadingList(readingList, items);
        SerializeV1(cbl, filePath);

        return filePath;
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
                Number = item.Chapter.Range,
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
