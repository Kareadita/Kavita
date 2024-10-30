using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using API.DTOs.CoverDb;
using API.Entities;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace API.Data.Repositories;
#nullable enable

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

    public CoverDbAuthor? FindBestAuthorMatch(Person person)
    {
        var aniListId = person.AniListId > 0 ? $"{person.AniListId}" : string.Empty;
        var highestScore = 0;
        CoverDbAuthor? bestMatch = null;

        foreach (var author in _authors)
        {
            var score = 0;

            // Check metadata IDs and add points if they match
            if (!string.IsNullOrEmpty(author.Ids.AmazonId) && author.Ids.AmazonId == person.Asin)
            {
                score += 10;
            }
            if (!string.IsNullOrEmpty(author.Ids.AnilistId) && author.Ids.AnilistId == aniListId)
            {
                score += 10;
            }
            if (!string.IsNullOrEmpty(author.Ids.HardcoverId) && author.Ids.HardcoverId == person.HardcoverId)
            {
                score += 10;
            }

            // Check for exact name match
            if (author.Name.Equals(person.Name, StringComparison.OrdinalIgnoreCase))
            {
                score += 7;
            }

            // Check for alias match
            if (author.Aliases.Contains(person.Name, StringComparer.OrdinalIgnoreCase))
            {
                score += 5;
            }

            // Update the best match if current score is higher
            if (score <= highestScore) continue;

            highestScore = score;
            bestMatch = author;
        }

        return bestMatch;
    }

}
