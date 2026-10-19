using System.Text.Json.Serialization;

namespace Kavita.Models.DTOs.Kobo;

public sealed record KoboDownloadUrlDto
{
    public string DrmType { get; set; } = "None";
    public KoboFormat Format { get; set; }
    public long Size { get; set; }
    public string Platform { get; set; } = "Generic";
    public string Url { get; set; }
}

public enum KoboFormat
{
    [JsonStringEnumMemberName("EPUB3FL")]
    Epub3Fl,
    [JsonStringEnumMemberName("EPUB")]
    Epub,
    [JsonStringEnumMemberName("EPUB3")]
    Epub3,
    [JsonStringEnumMemberName("KEPUB")]
    Kepub,
}
