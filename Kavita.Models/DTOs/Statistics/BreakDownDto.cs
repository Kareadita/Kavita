using System.Collections.Generic;

namespace Kavita.Models.DTOs.Statistics;

public sealed record BreakDownDto<T>
{

    public IList<StatCount<T>> Data { get; set; }

    public int Total { get; set; }
    public int TotalOptions  { get; set; }
    public int Missing { get; set; }

}
