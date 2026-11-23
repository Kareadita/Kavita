namespace Kavita.Models.Kobo;
#nullable enable

/// <summary>
/// A run of pages for one table of contents entry, before it is given a label
/// </summary>
/// <param name="FirstPage">0-based index of the first page in the group</param>
/// <param name="LastPage">0-based index of the last page in the group, inclusive</param>
/// <param name="Kind">What the pages were grouped by</param>
/// <param name="Number">Chapter number as parsed ("1", "10.5") or 1-based folder index; null for <see cref="EpubTocGroupKind.Pages"/></param>
public sealed record EpubTocGroup(int FirstPage, int LastPage, EpubTocGroupKind Kind, string? Number);
