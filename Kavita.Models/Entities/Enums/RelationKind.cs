using System.ComponentModel;

namespace Kavita.Models.Entities.Enums;

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
    Annual = 14,
    #region MangaBaka Only
    /// <summary>
    /// MangaBaka <c>main</c>: the parent/main story this entry branches from.
    /// </summary>
    [Description("Main Story")]
    Main = 15,
    /// <summary>
    /// MangaBaka <c>cameo</c>: a brief appearance of characters from another series.
    /// </summary>
    [Description("Cameo")]
    Cameo = 16,
    /// <summary>
    /// MangaBaka <c>character_focus</c>: focuses on characters from the related series.
    /// </summary>
    [Description("Character Focus")]
    CharacterFocus = 17,
    /// <summary>
    /// MangaBaka <c>compilation</c>: a compilation/collection of the related series.
    /// </summary>
    [Description("Compilation")]
    Compilation = 18,
    /// <summary>
    /// MangaBaka <c>crossover</c>: a crossover between series.
    /// </summary>
    [Description("Crossover")]
    Crossover = 19,
    /// <summary>
    /// MangaBaka <c>expansion</c>: an expanded version of the related series.
    /// </summary>
    [Description("Expansion")]
    Expansion = 20,
    /// <summary>
    /// MangaBaka <c>parody</c>: a parody of the related series.
    /// </summary>
    [Description("Parody")]
    Parody = 21,
    /// <summary>
    /// MangaBaka <c>reboot</c>: a reboot of the related series.
    /// </summary>
    [Description("Reboot")]
    Reboot = 22,
    /// <summary>
    /// MangaBaka <c>remake</c>: a remake of the related series.
    /// </summary>
    [Description("Remake")]
    Remake = 23,
    /// <summary>
    /// MangaBaka <c>series</c>: belongs to the same overall series.
    /// </summary>
    [Description("Series")]
    Series = 24,
    /// <summary>
    /// MangaBaka <c>source</c>: the source work this entry adapts.
    /// </summary>
    [Description("Source")]
    Source = 25,
    /// <summary>
    /// MangaBaka <c>summary</c>: a summary/abridged version of the related series.
    /// </summary>
    [Description("Summary")]
    Summary = 26,
    /// <summary>
    /// MangaBaka <c>uncollected</c>: uncollected chapters related to the series.
    /// </summary>
    [Description("Uncollected")]
    Uncollected = 27

    #endregion

}
