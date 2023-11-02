namespace API.DTOs.Filtering.v2;

/// <summary>
/// For requesting an encoded filter to be decoded
/// </summary>
public class DecodeFilterDto
{
    public string EncodedFilter { get; set; }
}
