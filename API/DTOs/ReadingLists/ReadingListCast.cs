using System.Collections.Generic;
using API.DTOs.Person;

namespace API.DTOs.ReadingLists;

public sealed record ReadingListCast
{
    public ICollection<PersonDto> Writers { get; set; } = [];
    public ICollection<PersonDto> CoverArtists { get; set; } = [];
    public ICollection<PersonDto> Publishers { get; set; } = [];
    public ICollection<PersonDto> Characters { get; set; } = [];
    public ICollection<PersonDto> Pencillers { get; set; } = [];
    public ICollection<PersonDto> Inkers { get; set; } = [];
    public ICollection<PersonDto> Imprints { get; set; } = [];
    public ICollection<PersonDto> Colorists { get; set; } = [];
    public ICollection<PersonDto> Letterers { get; set; } = [];
    public ICollection<PersonDto> Editors { get; set; } = [];
    public ICollection<PersonDto> Translators { get; set; } = [];
    public ICollection<PersonDto> Teams { get; set; } = [];
    public ICollection<PersonDto> Locations { get; set; } = [];
}
