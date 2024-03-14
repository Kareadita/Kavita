namespace API.Entities.Enums;

public enum PersonRole
{
    /// <summary>
    /// Another role, not covered by other types
    /// </summary>
    Other = 1,
    /// <summary>
    /// Author or Writer
    /// </summary>
    Writer = 3,
    Penciller = 4,
    Inker = 5,
    Colorist = 6,
    Letterer = 7,
    CoverArtist = 8,
    Editor = 9,
    Publisher = 10,
    /// <summary>
    /// Represents a character/person within the story
    /// </summary>
    Character = 11,
    /// <summary>
    /// The Translator
    /// </summary>
    Translator = 12,
    /// <summary>
    /// The publisher before another Publisher bought
    /// </summary>
    Imprint = 13,
    Team = 14,
    Location = 15
}
