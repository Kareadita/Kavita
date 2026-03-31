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
    private int _nextOrder;

    private CblFileBuilder(string name)
    {
        _name = name;
    }

    public static CblFileBuilder Create(string name) => new(name);

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
            SchemaVersion = 1,
            Name = _name,
            Items = new List<ParsedCblItem>(_items)
        };
    }
}
