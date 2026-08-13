namespace Kavita.Models.DTOs.KavitaPlus.Metadata;
#nullable enable

public sealed record ALMediaTitle
{
    public string? EnglishTitle { get; set; }
    public string RomajiTitle { get; set; }
    public string NativeTitle { get; set; }
    public string PreferredTitle { get; set; }
}
