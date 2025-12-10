using System.Collections.Generic;

namespace API.DTOs.Statistics;

public sealed record BreakDownDto<T>
{

    public IList<StatCount<T>> Data { get; set; }

    public int Total { get; set; }
    public int TotalOptions  { get; set; }
    public int Missing { get; set; }

}
