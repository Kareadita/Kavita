using System;
using System.Collections.Generic;

namespace API.DTOs.Statistics;

public sealed record ReadTimeByHourDto
{

    public DateTime DataSince { get; init; }
    public IList<StatCount<int>> Stats { get; init; }

}
