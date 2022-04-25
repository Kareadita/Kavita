using System.ComponentModel;

namespace API.Entities.Enums;

public enum PublicationStatus
{
    /// <summary>
    /// Default Status. Publication is currently in progress
    /// </summary>
    [Description("Ongoing")]
    OnGoing = 0,
    /// <summary>
    /// Series is on temp or indefinite Hiatus
    /// </summary>
    [Description("Hiatus")]
    Hiatus = 1,
    /// <summary>
    /// Publication has finished releasing
    /// </summary>
    [Description("Completed")]
    Completed = 2,
    /// <summary>
    /// Publication has been cancelled
    /// </summary>
    [Description("Cancelled")]
    Cancelled = 3,
    /// <summary>
    /// Publication has been completed, but Kavita doesn't have all issues/volumes accounted for
    /// </summary>
    [Description("Ended")]
    Ended = 4

}
