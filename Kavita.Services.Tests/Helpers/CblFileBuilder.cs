using Kavita.Models.DTOs.ReadingLists.CBL;
using Kavita.Models.DTOs.ReadingLists.CBL.Internal;

namespace Kavita.Services.Tests.Helpers;

/// <summary>
/// Fluent builder for constructing <see cref="ParsedCblReadingList"/> objects in tests
/// </summary>
public class CblFileBuilder
{
    private readonly string _name;
    private readonly List<ParsedCblItem> _items = [];
    private readonly List<string> _tags = [];
    private int _nextOrder;
    private int _schemaVersion = 1;

    private CblFileBuilder(string name)
    {
        _name = name;
    }

    public static CblFileBuilder Create(string name) => new(name);

    public CblFileBuilder AsV2()
    {
        _schemaVersion = 2;
        return this;
    }

    public CblFileBuilder WithTags(params string[] tags)
    {
        _tags.AddRange(tags);
        return this;
    }

    public CblFileBuilder AddBook(string series, string volume = "", string number = "",
        string year = "", List<CblExternalId>? externalIds = null)
    {
        _items.Add(new ParsedCblItem
        {
            Order = _nextOrder++,
            SeriesName = series,
            Volume = volume,
            Number = number,
            Year = year,
            ExternalIds = externalIds ?? []
        });
        return this;
    }

    public ParsedCblReadingList Build()
    {
        return new ParsedCblReadingList
        {
            SchemaVersion = _schemaVersion,
            Name = _name,
            Items = new List<ParsedCblItem>(_items),
            Tags = new List<string>(_tags)
        };
    }
}
