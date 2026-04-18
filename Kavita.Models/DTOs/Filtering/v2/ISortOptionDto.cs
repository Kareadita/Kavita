using System;

namespace Kavita.Models.DTOs.Filtering.v2;


public interface ISortOptionDto<TField> where TField : struct, Enum
{
    TField SortField { get; set; }
    bool IsAscending { get; set; }
}
