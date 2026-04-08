using System;

namespace Kavita.Models.DTOs.ReadingLists;
#nullable enable

public sealed record ReadingListItemChapterDto
{
    public int Id { get; init; }
    public string Range { get; init; } = string.Empty;
    public string? TitleName { get; init; }
    public float MinNumber { get; init; }
    public float MaxNumber { get; init; }
    public float SortOrder { get; init; }
    public int Pages { get; init; }
    public bool IsSpecial { get; init; }
    public DateTime ReleaseDate { get; init; }
    public string? Summary { get; init; }
    public string? WriterName { get; init; }
    public int? WriterId { get; init; }
    public string? PencillerName { get; init; }
    public int? PencillerId { get; init; }
}
