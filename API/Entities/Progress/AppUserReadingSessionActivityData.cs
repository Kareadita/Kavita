using System;
using System.Collections.Generic;
using API.DTOs.Progress;
using API.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace API.Entities.Progress;
#nullable enable

public class AppUserReadingSessionActivityData
{
    public int Id { get; set; }
    public int AppUserReadingSessionId { get; set; }
    public AppUserReadingSession ReadingSession { get; set; }

    public MangaFormat Format { get; set; }
    public int ChapterId { get; set; }
    public virtual Chapter Chapter { get; set; }
    public int VolumeId { get; set; }
    public virtual Volume Volume { get; set; }
    public int SeriesId { get; set; }
    public virtual Series Series { get; set; }
    public int LibraryId { get; set; }
    public virtual Library Library { get; set; }
    public int StartPage { get; set; }
    public int EndPage { get; set; }
    public string? StartBookScrollId { get; set; }
    public string? EndBookScrollId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime? EndTime { get; set; }
    public DateTime? EndTimeUtc { get; set; }
    public int PagesRead { get; set; }
    /// <summary>
    /// Only applicable for Book entries
    /// </summary>
    public int WordsRead { get; set; }
    /// <summary>
    /// Total Pages at the time of reading
    /// </summary>
    /// <remarks>This can skew over time when files are updated/replaced</remarks>
    public int TotalPages { get; set; }
    /// <summary>
    /// Total Words at the time of reading
    /// </summary>
    /// <remarks>This can skew over time when files are updated/replaced</remarks>
    public long TotalWords { get; set; }
    /// <summary>
    /// Client information for this reading activity.
    /// Tracks device, browser, and authentication details.
    /// </summary>
    public ClientInfoData? ClientInfo { get; set; }
    /// <summary>
    /// List of device PKs that connected during this reading session
    /// </summary>
    /// <remarks>JSON Column</remarks>
    public List<int> DeviceIds { get; set; } = [];

    public AppUserReadingSessionActivityData()
    {
    }

    public AppUserReadingSessionActivityData(ProgressDto dto, int startPage, MangaFormat format)
    {
        ChapterId = dto.ChapterId;
        VolumeId = dto.VolumeId;
        SeriesId = dto.SeriesId;
        LibraryId = dto.LibraryId;
        StartPage = startPage;
        StartBookScrollId = dto.BookScrollId;
        EndPage = dto.PageNum;
        StartTime = DateTime.Now;
        StartTimeUtc = DateTime.UtcNow;
        EndTime = null;
        EndTimeUtc = null;
        PagesRead = 0;
        WordsRead = 0;
        ClientInfo = null;
        DeviceIds = [];
        Format = format;

    }
}
