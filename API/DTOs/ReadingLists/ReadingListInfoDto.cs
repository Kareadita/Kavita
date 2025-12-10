using API.Entities.Interfaces;

namespace API.DTOs.ReadingLists;

public sealed record ReadingListInfoDto : IHasReadTimeEstimate
{
    /// <summary>
    /// Total Pages across all Reading List Items
    /// </summary>
    public int Pages { get; set; }
    /// <summary>
    /// Total Word count across all Reading List Items
    /// </summary>
    public long WordCount { get; set; }
    /// <summary>
    /// Are ALL Reading List Items epub
    /// </summary>
    public bool IsAllEpub { get; set; }
    /// <inheritdoc cref="IHasReadTimeEstimate.MinHoursToRead"/>
    public int MinHoursToRead { get; set; }
    /// <inheritdoc cref="IHasReadTimeEstimate.MaxHoursToRead"/>
    public int MaxHoursToRead { get; set; }
    /// <inheritdoc cref="IHasReadTimeEstimate.AvgHoursToRead"/>
    public float AvgHoursToRead { get; set; }
}
