using System.ComponentModel;

namespace Kavita.Models.DTOs.Filtering.v2;

/// <summary>
/// Represents the underlying entity which drives marshaling
/// </summary>
public enum FilterEntityType
{
    [Description("Series")]
    Series = 0,
    [Description("Reading List")]
    ReadingList = 1,
    [Description("Person")]
    Person = 2,
    [Description("Annotation")]
    Annotation = 3
}
