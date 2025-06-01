namespace API.DTOs.Filtering.v2;

/// <summary>
/// For requesting an encoded filter to be decoded
/// </summary>
public sealed record DecodeFilterDto
{
    public string EncodedFilter { get; set; }
}
