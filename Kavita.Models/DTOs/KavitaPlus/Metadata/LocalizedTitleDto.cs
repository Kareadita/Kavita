namespace Kavita.Models.DTOs.KavitaPlus.Metadata;

public sealed record LocalizedTitleDto
{
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// The provider's preferred title within this language.
    /// </summary>
    public bool IsPrimary { get; init; }

    /// <summary>
    /// An officially licensed title, as opposed to a fan or community translation.
    /// </summary>
    public bool IsOfficial { get; init; }
}
