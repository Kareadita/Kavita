using System;
using System.Collections.Generic;
using API.Entities.Interfaces;
using API.Services.Reading;
using Microsoft.EntityFrameworkCore;

namespace API.Entities.Progress;

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
