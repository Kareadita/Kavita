using System;
using System.Collections.Generic;
using API.Data.Misc;
using API.Entities.Enums.Device;

namespace API.DTOs.Stats.V3;

public class UserStatV3
{
    public AgeRestriction AgeRestriction { get; set; }
    /// <summary>
    /// The last reading progress on the server (in UTC)
    /// </summary>
    public DateTime LastReadTime { get; set; }
    /// <summary>
    /// The last login on the server (in UTC)
    /// </summary>
    public DateTime LastLogin { get; set; }
    /// <summary>
    /// Has the user gone through email confirmation
    /// </summary>
    public bool IsEmailConfirmed { get; set; }
    /// <summary>
    /// Is the Email a valid address
    /// </summary>
    public bool HasValidEmail { get; set; }
    /// <summary>
    /// Float between 0-1 to showcase how much of the libraries a user has access to
    /// </summary>
    public float PercentageOfLibrariesHasAccess { get; set; }
    /// <summary>
    /// Number of reading lists this user created
    /// </summary>
    public int ReadingListsCreatedCount { get; set; }
    /// <summary>
    /// Number of collections this user created
    /// </summary>
    public int CollectionsCreatedCount { get; set; }
    /// <summary>
    /// Number of series in want to read for this user
    /// </summary>
    public int WantToReadSeriesCount { get; set; }
    /// <summary>
    /// Active locale for the user
    /// </summary>
    public string Locale { get; set; }
    /// <summary>
    /// Active Theme (name)
    /// </summary>
    public string ActiveTheme { get; set; }
    /// <summary>
    /// Number of series with Bookmarks created
    /// </summary>
    public int SeriesBookmarksCreatedCount { get; set; }
    /// <summary>
    /// Kavita+ only - Has an AniList Token set
    /// </summary>
    public bool HasAniListToken { get; set; }
    /// <summary>
    /// Kavita+ only - Has a MAL Token set
    /// </summary>
    public bool HasMALToken { get; set; }
    /// <summary>
    /// Number of Smart Filters a user has created
    /// </summary>
    public int SmartFilterCreatedCount { get; set; }
    /// <summary>
    /// Is the user sharing reviews
    /// </summary>
    public bool IsSharingReviews { get; set; }
    /// <summary>
    /// The number of devices setup and their platforms
    /// </summary>
    public ICollection<DevicePlatform> DevicePlatforms { get; set; }
    /// <summary>
    /// Roles for this user
    /// </summary>
    public ICollection<string> Roles { get; set; }


}
