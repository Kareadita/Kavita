using System;

namespace Kavita.Models.DTOs.Misc;

public record GithubRateLimitDto
{
    public int? Remaining { get; set; }
    public int? Limit { get; set; }
    public DateTime? ResetsAtUtc { get; set; }

    /// <summary>
    /// Threshold below which we warn the user proactively
    /// </summary>
    public bool IsLow => Remaining is not null and <= 10;
    public bool IsExhausted => Remaining is not null and <= 0;
}
