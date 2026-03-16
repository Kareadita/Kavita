using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Kavita.Models.DTOs.CoverDb;

public sealed record CoverDbPeople
{
    [YamlMember(Alias = "people", ApplyNamingConventions = false)]
    public List<CoverDbAuthor> People { get; set; } = new List<CoverDbAuthor>();
}
