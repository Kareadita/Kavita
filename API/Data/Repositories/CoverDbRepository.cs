using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using API.DTOs.CoverDb;
using API.Entities;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace API.Data.Repositories;

/// <summary>
/// This is a manual repository, not a DB repo
/// </summary>
public class CoverDbRepository
{
    private readonly List<CoverDbAuthor> _authors;

    public CoverDbRepository(string filePath)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        // Read and deserialize YAML file
        var yamlContent = File.ReadAllText(filePath);
        var peopleData = deserializer.Deserialize<CoverDbPeople>(yamlContent);
        _authors = peopleData.People;
    }

    public CoverDbAuthor? FindAuthorByNameOrAlias(string name)
    {
        return _authors.Find(author =>
            author.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            author.Aliases.Contains(name, StringComparer.OrdinalIgnoreCase));
    }

    public CoverDbAuthor? FindAuthorByAny(Person person)
    {
        var aniListId = person.AniListId > 0 ? $"{person.AniListId}" : string.Empty;

        return _authors.Find(author =>
            author.Name.Equals(person.Name, StringComparison.OrdinalIgnoreCase) ||
            author.Aliases.Contains(person.Name, StringComparer.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(author.Ids.AmazonId) && author.Ids.AmazonId == person.Asin) ||
            (!string.IsNullOrEmpty(author.Ids.AnilistId) && author.Ids.AnilistId == aniListId) ||
            (!string.IsNullOrEmpty(author.Ids.HardcoverId) && author.Ids.HardcoverId == person.HardcoverId)
        );
    }
}
