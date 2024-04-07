using System.ComponentModel;

namespace API.Entities.Enums;

/// <summary>
/// Represents a relationship between Series
/// </summary>
public enum RelationKind
{
    /// <summary>
    /// Story that occurred before the original.
    /// </summary>
    [Description("Prequel")]
    Prequel = 1,
    /// <summary>
    /// Direct continuation of the story.
    /// </summary>
    [Description("Sequel")]
    Sequel = 2,
    /// <summary>
    /// Uses characters of a different series, but is not an alternate setting or story.
    /// </summary>
    [Description("Spin Off")]
    SpinOff = 3,
    /// <summary>
    /// Manga/Anime/Light Novel adaptation
    /// </summary>
    [Description("Adaptation")]
    Adaptation = 4,
    /// <summary>
    /// Takes place sometime during the parent storyline.
    /// </summary>
    [Description("Side Story")]
    SideStory = 5,
    /// <summary>
    /// When characters appear in both series, but is not a spin-off
    /// </summary>
    [Description("Character")]
    Character = 6,
    /// <summary>
    /// When the story contains another story, useful for One-Shots
    /// </summary>
    [Description("Contains")]
    Contains = 7,
    /// <summary>
    /// When nothing else fits
    /// </summary>
    [Description("Other")]
    Other = 8,
    /// <summary>
    /// Same universe/world/reality/timeline, completely different characters
    /// </summary>
    [Description("Alternative Setting")]
    AlternativeSetting = 9,
    /// <summary>
    /// Same setting, same characters, story is told differently
    /// </summary>
    [Description("Alternative Version")]
    AlternativeVersion = 10,
    /// <summary>
    /// Doujinshi or Fan work
    /// </summary>
    [Description("Doujinshi")]
    Doujinshi = 11,
    /// <summary>
    /// This is a UI field only. Not to be used in backend
    /// </summary>
    [Description("Parent")]
    Parent = 12,
    /// <summary>
    /// Same story, could be translation, colorization... Different edition of the series
    /// </summary>
    [Description("Edition")]
    Edition = 13,
    /// <summary>
    /// The target series is an annual of the Series
    /// </summary>
    [Description("Annual")]
    Annual = 14

}
