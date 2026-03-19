using System;
using System.Collections.Generic;
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Entities.User;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Models.Entities.Progress;

/// <summary>
/// Represents a reading session for a user. See <see cref="ReadingSessionService"/>
/// </summary>
[Index(nameof(IsActive), IsUnique = false)]
public class AppUserReadingSession : IEntityDate
{
    public int Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime? EndTime { get; set; }
    public DateTime? EndTimeUtc { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>
    /// True if the session was generated and not from progress events. (I.e. mark as read)
    /// </summary>
    public bool IsGenerated { get; set; } = false;
    /// <summary>
    /// Actual activity data recorded during the session
    /// </summary>
    public IList<AppUserReadingSessionActivityData> ActivityData { get; set; }
    public DateTime Created { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime LastModifiedUtc { get; set; }


    public int AppUserId { get; set; }
    public virtual AppUser AppUser { get; set; }
}
