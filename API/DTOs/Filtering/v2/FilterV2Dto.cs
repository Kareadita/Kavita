using System;
using System.Collections.Generic;
using System.Linq;
using API.DTOs.Metadata;
using API.Extensions.QueryExtensions.Filtering;

namespace API.DTOs.Filtering.v2;



/// <summary>
/// Metadata filtering for v2 API only
/// </summary>
public class FilterV2Dto
{
    /// <summary>
    /// The name of the filter if not anonymous
    /// </summary>
    public string? Name { get; set; }
    public ICollection<FilterGroupDto> Groups { get; set; }
    public SortOptions SortOptions { get; set; } // TODO: Solve for how to do this and have it serializable to DB

    /// <summary>
    /// Limit the number of rows returned. Defaults to not applying a limit (aka 0)
    /// </summary>
    public int LimitTo { get; set; } = 0;
}

public class FilterGroupDto
{
    public ICollection<FilterGroupDto> And { get; set; }
    public ICollection<FilterGroupDto> Or { get; set; }
    /// <summary>
    /// If there are statements then it is assumed there are no And/Ors
    /// </summary>
    public ICollection<FilterStatementDto> Statements { get; set; }
}




