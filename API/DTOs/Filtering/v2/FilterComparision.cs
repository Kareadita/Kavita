using System.ComponentModel;

namespace API.DTOs.Filtering.v2;

public enum FilterComparison
{
    [Description("Equal")]
    Equal = 0,
    GreaterThan = 1,
    GreaterThanEqual = 2,
    LessThan = 3,
    LessThanEqual = 4,
    /// <summary>
    /// value is within any of the series. This is inheritently an OR, even if combinator is an AND
    /// </summary>
    /// <remarks>Only works with IList</remarks>
    Contains = 5,
    /// <summary>
    /// value is within All of the series. This is an AND, even if combinator ORs the different statements
    /// </summary>
    /// <remarks>Only works with IList</remarks>
    MustContains = 6,
    /// <summary>
    /// Performs a LIKE %value%
    /// </summary>
    Matches = 7,
    NotContains = 8,
    /// <summary>
    /// Not Equal to
    /// </summary>
    NotEqual = 9,
    /// <summary>
    /// String starts with
    /// </summary>
    BeginsWith = 10,
    /// <summary>
    /// String ends with
    /// </summary>
    EndsWith = 11,
    /// <summary>
    /// Is Date before X
    /// </summary>
    IsBefore = 12,
    /// <summary>
    /// Is Date after X
    /// </summary>
    IsAfter = 13,
    /// <summary>
    /// Is Date between now and X seconds ago
    /// </summary>
    IsInLast = 14,
    /// <summary>
    /// Is Date not between now and X seconds ago
    /// </summary>
    IsNotInLast = 15,
    /// <summary>
    /// There are no records
    /// </summary>
    IsEmpty = 16
}
