namespace API.DTOs.KavitaPlus.Manage;

/// <summary>
/// Represents an option in the UI layer for Filtering
/// </summary>
public enum MatchStateOption
{
    All = 0,
    Matched = 1,
    NotMatched = 2,
    Error = 3,
    DontMatch = 4
}

public sealed record ManageMatchFilterDto
{
    public MatchStateOption MatchStateOption { get; set; } = MatchStateOption.All;
    /// <summary>
    /// Library Type in int form. -1 indicates to ignore the field.
    /// </summary>
    public int LibraryType { get; set; } = -1;
    public string SearchTerm { get; set; } = string.Empty;
}
