namespace API.DTOs.KavitaPlus.Metadata;

public enum CharacterRole
{
    Main = 0,
    Supporting = 1,
    Background = 2
}


public class SeriesCharacter
{
    public string Name { get; set; }
    public required string Description { get; set; }
    public required string Url { get; set; }
    public string? ImageUrl { get; set; }
    public CharacterRole Role { get; set; }
}
