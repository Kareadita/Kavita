using System.Collections.Generic;
using Kavita.Models.Entities.Enums;
using System.ComponentModel.DataAnnotations;
using Kavita.Models.Attributes;
using Kavita.Models.Entities.Interfaces;

namespace Kavita.Models.DTOs.Person;
#nullable enable

public record PersonDto : IHasCoverImageDto
{
    public int Id { get; set; }
    public required string Name { get; set; }

    public bool HasCoverImage { get; set; }
    public bool CoverImageLocked { get; set; }
    public List<string> Aliases { get; set; } = [];

    public string? Description { get; set; }
    /// <summary>
    /// ASIN for person
    /// </summary>
    /// <remarks>Can be used for Amazon author lookup</remarks>
    public string? Asin { get; set; }

    /// <summary>
    /// https://anilist.co/staff/{AniListId}/
    /// </summary>
    /// <remarks>Kavita+ Only</remarks>
    public int AniListId { get; set; } = 0;
    /// <summary>
    /// https://myanimelist.net/people/{MalId}/
    /// https://myanimelist.net/character/{MalId}/CharacterName
    /// </summary>
    /// <remarks>Kavita+ Only</remarks>
    public long MalId { get; set; } = 0;
    /// <summary>
    /// https://hardcover.app/authors/{HardcoverId}
    /// </summary>
    /// <remarks>Kavita+ Only</remarks>
    public string? HardcoverId { get; set; }

    /// <summary>
    /// Web links derived from the various id of external websites
    /// </summary>
    /// <remarks>Only present when retrieving from person info endpoint</remarks>
    public IList<string>? WebLinks { get; set; } = [];
    /// <summary>
    /// All roles as if returned by the /api/person/roles endpoint
    /// </summary>
    /// <remarks>Only present when retrieving from person info endpoint</remarks>
    [EnumCollection(typeof(PersonRole))]
    public IList<PersonRole>? Roles { get; set; } = [];
}
