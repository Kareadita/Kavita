using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.Progress;
#nullable enable

/// <summary>
/// Reading that happened earlier and is only now being reported, e.g. by a client that was offline while reading
/// </summary>
public sealed record BackfillReadingDto
{
    /// <summary>
    /// At most <see cref="MaxEntries"/>. Clients with more should send several requests, cutting them where the reading
    /// paused for longer than a session lasts (10 minutes), since entries in different requests are never joined
    /// into one session
    /// </summary>
    public required IList<BackfillReadingEntryDto> Entries { get; set; }

    public const int MaxEntries = 100;
}

/// <summary>
/// One uninterrupted stretch of reading a single chapter
/// </summary>
public sealed record BackfillReadingEntryDto
{
    public required int ChapterId { get; set; }
    /// <summary>
    /// Page the stretch started on, in the same terms as <see cref="ProgressDto.PageNum"/>
    /// </summary>
    public required int StartPage { get; set; }
    /// <summary>
    /// Page the stretch ended on. This is also where the reader left the chapter
    /// </summary>
    public required int EndPage { get; set; }
    /// <summary>
    /// For EPUB, where in the start page the stretch began. Used to count the words read
    /// </summary>
    public string? StartBookScrollId { get; set; }
    /// <summary>
    /// For EPUB, where the reader left the chapter
    /// </summary>
    public string? EndBookScrollId { get; set; }
    /// <summary>
    /// When the client started reading
    /// </summary>
    public required DateTime StartTimeUtc { get; set; }
    /// <summary>
    /// When the client stopped reading
    /// </summary>
    public required DateTime EndTimeUtc { get; set; }
}

public enum BackfillReadingStatus
{
    /// <summary>
    /// The reading was recorded
    /// </summary>
    Applied = 0,
    /// <summary>
    /// The same reading had already been recorded, so nothing changed. The client can drop it
    /// </summary>
    AlreadyRecorded = 1,
    /// <summary>
    /// The user cannot read the chapter, or it no longer exists
    /// </summary>
    NoAccess = 2,
    /// <summary>
    /// Ends before it starts, or has negative pages
    /// </summary>
    Invalid = 3,
    /// <summary>
    /// Older than <see cref="BackfillReadingResultDto.MaxAgeDays"/> days
    /// </summary>
    TooOld = 4,
    /// <summary>
    /// Ends more than a few minutes after the server's clock
    /// </summary>
    InFuture = 5,
}

public sealed record BackfillReadingResultDto
{
    /// <summary>
    /// Entries older than this many days are not accepted
    /// </summary>
    public const int MaxAgeDays = 30;

    /// <summary>
    /// One status for each entry of the request, in the same order. Everything except
    /// <see cref="BackfillReadingStatus.Applied"/> and <see cref="BackfillReadingStatus.AlreadyRecorded"/> will be
    /// refused again if sent again
    /// </summary>
    [EnumDataType(typeof(BackfillReadingStatus))]
    public required IList<BackfillReadingStatus> Results { get; set; }
}
