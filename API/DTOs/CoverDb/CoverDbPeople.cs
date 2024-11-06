using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace API.DTOs.CoverDb;

public class CoverDbPeople
{
    [YamlMember(Alias = "people", ApplyNamingConventions = false)]
    public List<CoverDbAuthor> People { get; set; } = new List<CoverDbAuthor>();
}
