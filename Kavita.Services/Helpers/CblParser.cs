using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Xml.Serialization;
using Kavita.Models.DTOs.ReadingLists.CBL;
using Kavita.Models.DTOs.ReadingLists.CBL.Internal;
using Kavita.Models.DTOs.ReadingLists.CBL.V1;
using Kavita.Models.DTOs.ReadingLists.CBL.V2;
using Kavita.Models.Extensions;

namespace Kavita.Services.Helpers;

/// <summary>
/// Responsible for reading v1 and v2 specs into a common format
/// </summary>
public static class CblParser
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Auto-detect format by file extension and parse accordingly.
    /// </summary>
    public static ParsedCblReadingList Parse(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".cbl" or ".xml" => ParseV1(filePath),
            ".json" => ParseV2(filePath),
            _ => throw new ArgumentException($"Unsupported CBL file extension: {ext}")
        };
    }

    /// <summary>
    /// Parse a v1 XML CBL file into the unified model.
    /// </summary>
    public static ParsedCblReadingList ParseV1(string filePath)
    {
        var serializer = new XmlSerializer(typeof(CblReadingList));
        using var stream = File.OpenRead(filePath);
        var cbl = (CblReadingList)serializer.Deserialize(stream);

        var result = new ParsedCblReadingList
        {
            SchemaVersion = 1,
            Name = cbl.Name ?? string.Empty,
            Summary = cbl.Summary ?? string.Empty,
            StartYear = cbl.StartYear,
            StartMonth = cbl.StartMonth,
            EndYear = cbl.EndYear,
            EndMonth = cbl.EndMonth,
        };

        if (cbl.Books?.Book != null)
        {
            for (var i = 0; i < cbl.Books.Book.Count; i++)
            {
                var book = cbl.Books.Book[i];
                var item = new ParsedCblItem
                {
                    Order = i,
                    SeriesName = book.Series ?? string.Empty,
                    Number = book.Number ?? string.Empty,
                    Volume = book.Volume ?? string.Empty,
                    Year = book.Year ?? string.Empty,
                    Format = book.Format ?? string.Empty,
                    FileType = book.FileType ?? string.Empty,
                    IssueType = CblIssueType.Unknown,
                };

                foreach (var db in book.Databases)
                {
                    var provider = MapProviderName(db.Name);
                    item.ExternalIds.Add(new CblExternalId
                    {
                        Provider = provider,
                        SeriesId = db.Series ?? string.Empty,
                        IssueId = db.Issue ?? string.Empty,
                    });
                }

                result.Items.Add(item);
            }
        }

        return result;
    }

    /// <summary>
    /// Parse a v2 JSON CBL file into the unified model.
    /// </summary>
    /// <remarks>https://github.com/ComicReadingLists/json-cbl-standard/blob/main/schema/1.0/comic-reading-list.schema.json</remarks>
    public static ParsedCblReadingList ParseV2(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var v2 = JsonSerializer.Deserialize<CblV2Root>(json, JsonSerializerOptions);

        var result = new ParsedCblReadingList
        {
            Uuid = v2.FileDetails?.UUID ?? string.Empty,
            SchemaVersion = (int)(v2.FileDetails?.Version ?? 1),
            Name = v2.ListDetails?.Name ?? string.Empty,
            Summary = v2.ListDetails?.Description ?? string.Empty,
            Notes = v2.Notes ?? string.Empty,
            StartYear = v2.ListDetails?.StartYear ?? -1,
            StartMonth = -1,
            EndYear = v2.ListDetails?.EndYear ?? -1,
            EndMonth = -1,
            Publisher = v2.ListDetails?.Publisher ?? string.Empty,
            Imprint = v2.ListDetails?.Imprint ?? string.Empty,
            ListType = MapListType(v2.ListDetails?.Type),
            Tags = v2.ListDetails?.Tags ?? [],
            CoverImageUrls = v2.ListDetails?.CoverImageURLs ?? [],
        };

        if (v2.ListDetails?.Relationships != null)
        {
            foreach (var rel in v2.ListDetails.Relationships)
            {
                result.Relationships.Add(new CblRelationship
                {
                    Name = rel.Name ?? string.Empty,
                    Uuid = rel.UUID ?? string.Empty,
                    Relationship = rel.Relationship ?? string.Empty,
                });
            }
        }

        if (v2.ListDetails?.Source != null)
        {
            foreach (var src in v2.ListDetails.Source)
            {
                result.Sources.Add(new CblSource
                {
                    Name = src.Name ?? string.Empty,
                    Url = src.Url ?? string.Empty,
                });
            }
        }

        if (v2.IssueList != null)
        {
            for (var i = 0; i < v2.IssueList.Count; i++)
            {
                var issue = v2.IssueList[i];
                var item = new ParsedCblItem
                {
                    Order = i,
                    SeriesName = issue.SeriesName ?? string.Empty,
                    Number = issue.IssueNumber ?? string.Empty,
                    Volume = issue.SeriesStartYear?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    Year = ExtractYear(issue.IssueCoverDate),
                    CoverDate = issue.IssueCoverDate ?? string.Empty,
                    IssueType = MapIssueType(issue.IssueType),
                };

                if (issue.Id != null)
                {
                    foreach (var id in issue.Id)
                    {
                        item.ExternalIds.Add(new CblExternalId
                        {
                            Provider = MapProviderName(id.Name),
                            SeriesId = id.Series ?? string.Empty,
                            IssueId = id.Issue ?? string.Empty,
                        });
                    }
                }

                result.Items.Add(item);
            }
        }

        return result;
    }

    private static CblExternalDbProvider MapProviderName(string name)
    {
        return CblExternalDbProviderExtensions.FromName(name);
    }

    private static CblListType MapListType(string? type)
    {
        if (string.IsNullOrEmpty(type)) return CblListType.Unknown;

        return type.ToLowerInvariant() switch
        {
            "master" => CblListType.Master,
            "interuniversal" => CblListType.Interuniversal,
            "universal" => CblListType.Universal,
            "team" => CblListType.Team,
            "character" => CblListType.Character,
            "story" => CblListType.Story,
            _ => CblListType.Unknown,
        };
    }

    private static CblIssueType MapIssueType(string type)
    {
        if (string.IsNullOrEmpty(type)) return CblIssueType.Unknown;

        return type.ToLowerInvariant() switch
        {
            "event-core" => CblIssueType.EventCore,
            "event-tie-in" => CblIssueType.EventTieIn,
            "event-one-shot" => CblIssueType.EventOneShot,
            "ongoing" => CblIssueType.Ongoing,
            _ => CblIssueType.Unknown,
        };
    }

    private static string ExtractYear(string coverDate)
    {
        if (string.IsNullOrEmpty(coverDate)) return string.Empty;

        // Expected format: "YYYY-MM-DD"
        var dashIndex = coverDate.IndexOf('-');
        return dashIndex > 0 ? coverDate[..dashIndex] : coverDate;
    }

}
