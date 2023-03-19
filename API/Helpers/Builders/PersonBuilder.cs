using System.Collections.Generic;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;

namespace API.Helpers.Builders;

public class PersonBuilder : IEntityBuilder<Person>
{
    private readonly Person _person;
    public Person Build() => _person;

    public PersonBuilder(string name, PersonRole role)
    {
        _person = new Person()
        {
            Name = name.Trim(),
            NormalizedName = name.ToNormalized(),
            Role = role,
            ChapterMetadatas = new List<Chapter>(),
            SeriesMetadatas = new List<SeriesMetadata>()
        };
    }

    /// <summary>
    /// Only call for Unit Tests
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public PersonBuilder WithId(int id)
    {
        _person.Id = id;
        return this;
    }

    public PersonBuilder WithSeriesMetadata(SeriesMetadata metadata)
    {
        _person.SeriesMetadatas ??= new List<SeriesMetadata>();
        _person.SeriesMetadatas.Add(metadata);
        return this;
    }
}
