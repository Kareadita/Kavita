using System.ComponentModel;

namespace Kavita.Models.Entities.Enums.KavitaPlus;

public enum ScrobbleEventType
{
    [Description("Chapter Read")]
    ChapterRead = 0,
    [Description("Add to Want to Read")]
    AddWantToRead = 1,
    [Description("Remove from Want to Read")]
    RemoveWantToRead = 2,
    [Description("Score Updated")]
    ScoreUpdated = 3,
    [Description("Review Added/Updated")]
    Review = 4,
    [Description("Read status Updated")]
    ReadStatusUpdate = 5,
}
