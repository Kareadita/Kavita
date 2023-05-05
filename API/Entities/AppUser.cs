using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using API.Entities.Enums;
using API.Entities.Interfaces;
using Microsoft.AspNetCore.Identity;


namespace API.Entities;

public class AppUser : IdentityUser<int>, IHasConcurrencyToken
{
    public DateTime Created { get; set; } = DateTime.Now;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastActive { get; set; }
    public DateTime LastActiveUtc { get; set; }
    public ICollection<Library> Libraries { get; set; }
    public ICollection<AppUserRole> UserRoles { get; set; }
    public ICollection<AppUserProgress> Progresses { get; set; }
    public ICollection<AppUserRating> Ratings { get; set; }
    public AppUserPreferences UserPreferences { get; set; }
    /// <summary>
    /// Bookmarks associated with this User
    /// </summary>
    public ICollection<AppUserBookmark> Bookmarks { get; set; }
    /// <summary>
    /// Reading lists associated with this user
    /// </summary>
    public ICollection<ReadingList> ReadingLists { get; set; }
    /// <summary>
    /// A list of Series the user want's to read
    /// </summary>
    public ICollection<Series> WantToRead { get; set; }
    /// <summary>
    /// A list of Devices which allows the user to send files to
    /// </summary>
    public ICollection<Device> Devices { get; set; }
    /// <summary>
    /// An API Key to interact with external services, like OPDS
    /// </summary>
    public string ApiKey { get; set; }
    /// <summary>
    /// The confirmation token for the user (invite). This will be set to null after the user confirms.
    /// </summary>
    public string ConfirmationToken { get; set; }
    /// <summary>
    /// The highest age rating the user has access to. Not applicable for admins
    /// </summary>
    public AgeRating AgeRestriction { get; set; } = AgeRating.NotApplicable;
    /// <summary>
    /// If an age rating restriction is applied to the account, if Unknowns should be allowed for the user. Defaults to false.
    /// </summary>
    public bool AgeRestrictionIncludeUnknowns { get; set; } = false;

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
