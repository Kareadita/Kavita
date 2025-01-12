using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using API.Entities.Enums;
using API.Entities.Interfaces;
using API.Entities.Scrobble;
using Microsoft.AspNetCore.Identity;


namespace API.Entities;

public class AppUser : IdentityUser<int>, IHasConcurrencyToken
{
    public DateTime Created { get; set; } = DateTime.Now;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastActive { get; set; }
    public DateTime LastActiveUtc { get; set; }
    public ICollection<Library> Libraries { get; set; } = null!;
    public ICollection<AppUserRole> UserRoles { get; set; } = null!;
    public ICollection<AppUserProgress> Progresses { get; set; } = null!;
    public ICollection<AppUserRating> Ratings { get; set; } = null!;
    public AppUserPreferences UserPreferences { get; set; } = null!;
    /// <summary>
    /// Bookmarks associated with this User
    /// </summary>
    public ICollection<AppUserBookmark> Bookmarks { get; set; } = null!;
    /// <summary>
    /// Reading lists associated with this user
    /// </summary>
    public ICollection<ReadingList> ReadingLists { get; set; } = null!;
    /// <summary>
    /// Collections associated with this user
    /// </summary>
    public ICollection<AppUserCollection> Collections { get; set; } = null!;
    /// <summary>
    /// A list of Series the user want's to read
    /// </summary>
    public ICollection<AppUserWantToRead> WantToRead { get; set; } = null!;
    /// <summary>
    /// A list of Devices which allows the user to send files to
    /// </summary>
    public ICollection<Device> Devices { get; set; } = null!;
    /// <summary>
    /// A list of Table of Contents for a given Chapter
    /// </summary>
    public ICollection<AppUserTableOfContent> TableOfContents { get; set; } = null!;
    /// <summary>
    /// An API Key to interact with external services, like OPDS
    /// </summary>
    public string? ApiKey { get; set; }
    /// <summary>
    /// The confirmation token for the user (invite). This will be set to null after the user confirms.
    /// </summary>
    public string? ConfirmationToken { get; set; }
    /// <summary>
    /// The highest age rating the user has access to. Not applicable for admins
    /// </summary>
    public AgeRating AgeRestriction { get; set; } = AgeRating.NotApplicable;
    /// <summary>
    /// If an age rating restriction is applied to the account, if Unknowns should be allowed for the user. Defaults to false.
    /// </summary>
    public bool AgeRestrictionIncludeUnknowns { get; set; } = false;

    /// <summary>
    /// The JWT for the user's AniList account. Expires after a year.
    /// </summary>
    /// <remarks>Requires Kavita+ Subscription</remarks>
    public string? AniListAccessToken { get; set; }

    /// <summary>
    /// The Username of the MAL user
    /// </summary>
    public string? MalUserName { get; set; }
    /// <summary>
    /// The Client ID for the user's MAL account. User should create a client on MAL for this.
    /// </summary>
    public string? MalAccessToken { get; set; }



    /// <summary>
    /// A list of Series the user doesn't want scrobbling for
    /// </summary>
    public ICollection<ScrobbleHold> ScrobbleHolds { get; set; } = null!;
    /// <summary>
    /// A collection of user Smart Filters for their account
    /// </summary>
    public ICollection<AppUserSmartFilter> SmartFilters { get; set; } = null!;

    /// <summary>
    /// An ordered list of Streams (pre-configured) or Smart Filters that makes up the User's Dashboard
    /// </summary>
    public IList<AppUserDashboardStream> DashboardStreams { get; set; } = null!;
    /// <summary>
    /// An ordered list of Streams (pre-configured) or Smart Filters that makes up the User's SideNav
    /// </summary>
    public IList<AppUserSideNavStream> SideNavStreams { get; set; } = null!;
    public IList<AppUserExternalSource> ExternalSources { get; set; } = null!;


    /// <inheritdoc />
    [ConcurrencyCheck]
    public uint RowVersion { get; private set; }

    /// <inheritdoc />
    public void OnSavingChanges()
    {
        RowVersion++;
    }

    public void UpdateLastActive()
    {
        LastActive = DateTime.Now;
        LastActiveUtc = DateTime.UtcNow;
    }

}
