using System;

namespace API.DTOs.Misc;

public sealed record ParseResultDto
{
    public string SeriesName { get; set; }
    public string SeriesYear { get; set; }
    public float MinChapterNumber { get; set; }
    public  float MaxChapterNumber { get; set; }
    public float MinVolumeNumber { get; set; }
    public float MaxVolumeNumber { get; set; }


}
