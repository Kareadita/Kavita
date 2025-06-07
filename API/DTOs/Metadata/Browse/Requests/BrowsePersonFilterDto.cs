using System.Collections.Generic;
using API.DTOs.Filtering;
using API.Entities.Enums;

namespace API.DTOs.Metadata.Browse.Requests;
#nullable enable

public sealed record BrowsePersonFilterDto
{
    public required List<PersonRole> Roles { get; set; }
    public string? Query { get; set; }
    public PersonSortOptions? SortOptions { get; set; }
}
