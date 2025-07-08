namespace API.DTOs.Reader;

#nullable enable

public sealed record PersonalToCDto
{
    public required int Id { get; init; }
    public required int ChapterId { get; set; }
    /// <summary>
    /// The page to bookmark
    /// </summary>
    public required int PageNumber { get; set; }
    /// <summary>
    /// The title of the bookmark. Defaults to Page {PageNumber} if not set
    /// </summary>
    public required string Title { get; set; }
    /// <summary>
    /// For Book Reader, represents the nearest passed anchor on the screen that can be used to resume scroll point. If empty, the ToC point is the beginning of the page
    /// </summary>
    public string? BookScrollId { get; set; }
    /// <summary>
    /// Text of the bookmark
    /// </summary>
    public string? SelectedText { get; set; }
    /// <summary>
    /// Title of the Chapter this PToC was created in
    /// </summary>
    /// <remarks>Taken from the ToC</remarks>
    public string? ChapterTitle { get; set; }
}
