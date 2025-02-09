using System;
using System.Collections.Generic;
using API.Entities.Enums;

namespace API.DTOs.Stats.V3;

/// <summary>
/// Represents information about a Kavita Installation for Kavita Stats v3 API
/// </summary>
public class ServerInfoV3Dto
{
    /// <summary>
    /// Unique Id that represents a unique install
    /// </summary>
    public required string InstallId { get; set; }
    public required string Os { get; set; }
    /// <summary>
    /// If the Kavita install is using Docker
    /// </summary>
    public bool IsDocker { get; set; }
    /// <summary>
    /// Version of .NET instance is running
    /// </summary>
    public required string DotnetVersion { get; set; }
    /// <summary>
    /// Version of Kavita
    /// </summary>
    public required string KavitaVersion { get; set; }
    /// <summary>
    /// Version of Kavita on Installation
    /// </summary>
    public required string InitialKavitaVersion { get; set; }
    /// <summary>
    /// Date of first Installation
    /// </summary>
    public DateTime InitialInstallDate { get; set; }
    /// <summary>
    /// Number of Cores on the instance
    /// </summary>
    public int NumOfCores { get; set; }
    /// <summary>
    /// OS locale on the instance
    /// </summary>
    public string OsLocale { get; set; }
    /// <summary>
    /// Milliseconds to open a random archive (zip/cbz) for reading
    /// </summary>
    public long TimeToOpeCbzMs { get; set; }
    /// <summary>
    /// Number of pages for said archive (zip/cbz)
    /// </summary>
    public long TimeToOpenCbzPages { get; set; }
    /// <summary>
    /// Milliseconds to get a response from KavitaStats API
    /// </summary>
    /// <remarks>This pings a health check and does not capture any IP Information</remarks>
    public long TimeToPingKavitaStatsApi { get; set; }
    /// <summary>
    /// If using the downloading metadata feature
    /// </summary>
    /// <remarks>Kavita+ Only</remarks>
    public bool MatchedMetadataEnabled { get; set; }



    #region Media
    /// <summary>
    /// Number of collections on the install
    /// </summary>
    public int NumberOfCollections { get; set; }
    /// <summary>
    /// Number of reading lists on the install (Sum of all users)
    /// </summary>
    public int NumberOfReadingLists { get; set; }
    /// <summary>
    /// Total number of files in the instance
    /// </summary>
    public int TotalFiles { get; set; }
    /// <summary>
    /// Total number of Genres in the instance
    /// </summary>
    public int TotalGenres { get; set; }
    /// <summary>
    /// Total number of Series in the instance
    /// </summary>
    public int TotalSeries { get; set; }
    /// <summary>
    /// Total number of Libraries in the instance
    /// </summary>
    public int TotalLibraries { get; set; }
    /// <summary>
    /// Total number of People in the instance
    /// </summary>
    public int TotalPeople { get; set; }
    /// <summary>
    /// Max number of Series for any library on the instance
    /// </summary>
    public int MaxSeriesInALibrary { get; set; }
    /// <summary>
    /// Max number of Volumes for any library on the instance
    /// </summary>
    public int MaxVolumesInASeries { get; set; }
    /// <summary>
    /// Max number of Chapters for any library on the instance
    /// </summary>
    public int MaxChaptersInASeries { get; set; }
    /// <summary>
    /// Everything about the Libraries on the instance
    /// </summary>
    public IList<LibraryStatV3> Libraries { get; set; }
    /// <summary>
    /// Everything around Series Relationships between series
    /// </summary>
    public IList<RelationshipStatV3> Relationships { get; set; }
    #endregion

    #region Server
    /// <summary>
    /// Is OPDS enabled
    /// </summary>
    public bool OpdsEnabled { get; set; }
    /// <summary>
    /// The encoding the server is using to save media
    /// </summary>
    public EncodeFormat EncodeMediaAs { get; set; }
    /// <summary>
    /// The last user reading progress on the server (in UTC)
    /// </summary>
    public DateTime LastReadTime { get; set; }
    /// <summary>
    /// Is this server using Kavita+
    /// </summary>
    public bool ActiveKavitaPlusSubscription { get; set; }
    #endregion

    #region Users
    /// <summary>
    /// If there is at least one user that is using an age restricted profile on the instance
    /// </summary>
    /// <remarks>Introduced in v0.6.0</remarks>
    public bool UsingRestrictedProfiles { get; set; }

    public IList<UserStatV3> Users { get; set; }

    #endregion
}
