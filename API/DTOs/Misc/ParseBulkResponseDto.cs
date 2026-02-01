using System.Collections.Generic;

namespace API.DTOs.Misc;

public record ParseBulkResponseDto
{
    /// <summary>
    /// The requested name to the parsed result. Does not include errored items
    /// </summary>
    public Dictionary<string, ParseResultDto> Results { get; set; } = new();
    /// <summary>
    /// The requested name to parse maps to the Error exception
    /// </summary>
    public Dictionary<string, string> Errors { get; set; } = new();

    /// <summary>
    /// Count of errored items
    /// </summary>
    public int ErrorCounts => Errors.Count;
}
