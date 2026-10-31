using System.Collections.Generic;

namespace Kavita.Models.DTOs.Kobo;

public record KoboPagination<T>
{
    public List<T> Items { get; set; } = [];
    public int ItemCount { get; set; }
    public int TotalPageCount { get; set; }
    public int TotalItemCount { get; set; }
    public int CurrentPageIndex { get; set; }
    public int ItemsPerPage { get; set; }
    public int VersionCode { get; set; }
}

public sealed record KoboRecommendationResponseDto : KoboPagination<KoboBookWrapperDto<KoboBookRecommendationDto>>
{

    public static readonly KoboRecommendationResponseDto Empty = new()
    {
        Items = [],
        ItemCount = 0,
        TotalPageCount = 0,
        TotalItemCount = 0,
        CurrentPageIndex = 0,
        ItemsPerPage = 0,
        VersionCode = 0
    };

}

public sealed record KoboBookWrapperDto<T>
{
    public T Book { get; set; }
}
