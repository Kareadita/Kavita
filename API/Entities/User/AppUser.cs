#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using API.Entities.Enums;
using API.Entities.Interfaces;
using API.Entities.Progress;
using API.Entities.Scrobble;
using API.Entities.User;
using API.Helpers;
using Microsoft.AspNetCore.Identity;


namespace API.Entities;

public class AppUser : IdentityUser<int>, IHasConcurrencyToken, IHasCoverImage
{
    public DateTime Created { get; set; } = DateTime.Now;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastActive { get; set; }
    public DateTime LastActiveUtc { get; set; }
    public ICollection<Library> Libraries { get; set; } = null!;
    public ICollection<AppUserRole> UserRoles { get; set; } = null!;
    public ICollection<AppUserProgress> Progresses { get; set; } = null!;
    public ICollection<AppUserReadingSession> ReadingSessions { get; set; } = null!;
    public ICollection<AppUserReadingHistory> ReadingHistory { get; set; } = null!;
    public ICollection<AppUserRating> Ratings { get; set; } = null!;
    public ICollection<AppUserChapterRating> ChapterRatings { get; set; } = null!;
    public AppUserPreferences UserPreferences { get; set; } = null!;
    public ICollection<AppUserReadingProfile> ReadingProfiles { get; set; } = null!;
    public ICollection<ClientDevice> ClientDevices { get; set; } = null!;
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
    public ICollection<AppUserAnnotation> Annotations { get; set; } = null!;
    /// <summary>
    /// An API Key to interact with external services, like OPDS
    /// </summary>
    [Obsolete("Migrated to AuthKey in v0.8.9")]
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
    /// Has the user ran Scrobble Event Generation
    /// </summary>
    /// <remarks>Only applicable for Kavita+ and when a Token is present</remarks>
    public bool HasRunScrobbleEventGeneration { get; set; }
    /// <summary>
    /// The timestamp of when Scrobble Event Generation ran (Utc)
    /// </summary>
    /// <remarks>Kavita+ only</remarks>
    public DateTime ScrobbleEventGenerationRan { get; set; }

    /// <summary>
    /// The sub returned the by OIDC provider
    /// </summary>
    public string? OidcId { get; set; }
    /// <summary>
    /// The IdentityProvider for the user, default to <see cref="Enums.IdentityProvider.Kavita"/>
    /// </summary>
    public IdentityProvider IdentityProvider { get; set; } = IdentityProvider.Kavita;

    public string? CoverImage { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }


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
    /// <summary>
    /// Auth keys for access to Kavita
    /// </summary>
    public ICollection<AppUserAuthKey> AuthKeys { get; set; } = null!;


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

    public void ResetColorScape()
    {
        PrimaryColor = string.Empty;
        SecondaryColor = string.Empty;
    }

    public string GetOpdsAuthKey()
    {
        if (AuthKeys == null || AuthKeys.Count == 0)
        {
            throw new ArgumentNullException("AuthKeys not loaded");
        }

        return AuthKeys.Where(k => k.Name == AuthKeyHelper.OpdsKeyName).Select(k => k.Key).First();
    }

}
