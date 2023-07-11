using System;
using API.Entities.Interfaces;

namespace API.Entities.Scrobble;

/// <summary>
/// When a series is not found, we report it here
/// </summary>
public class ScrobbleError : IEntityDate
{
    public int Id { get; set; }

    /// <summary>
    /// Developer defined string
    /// </summary>
    public string Comment { get; set; }
    /// <summary>
    /// List of providers that could not
    /// </summary>
    public string Details { get; set; }

    public int SeriesId { get; set; }
    public Series Series { get; set; }

    public int LibraryId { get; set; }

    public int ScrobbleEventId { get; set; }
    public ScrobbleEvent ScrobbleEvent { get; set; }


    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }
}
