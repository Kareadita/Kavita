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

    public PersonBuilder(string name)
    {
        _person = new Person()
        {
            Name = name.Trim(),
            NormalizedName = name.ToNormalized(),
            SeriesMetadataPeople = new List<SeriesMetadataPeople>(),
            ChapterPeople = new List<ChapterPeople>()
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

    public PersonBuilder WithSeriesMetadata(SeriesMetadataPeople seriesMetadataPeople)
    {
        _person.SeriesMetadataPeople.Add(seriesMetadataPeople);
        return this;
    }

}
