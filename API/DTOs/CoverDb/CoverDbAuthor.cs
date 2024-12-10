using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace API.DTOs.CoverDb;

public class CoverDbAuthor
{
    [YamlMember(Alias = "name", ApplyNamingConventions = false)]
    public string Name { get; set; }
    [YamlMember(Alias = "aliases", ApplyNamingConventions = false)]
    public List<string> Aliases { get; set; } = new List<string>();
    [YamlMember(Alias = "ids", ApplyNamingConventions = false)]
    public CoverDbPersonIds Ids { get; set; }
    [YamlMember(Alias = "image_path", ApplyNamingConventions = false)]
    public string ImagePath { get; set; }
}
