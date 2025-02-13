using System;
using System.Collections.Generic;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;

namespace API.Helpers.Builders;

public class SeriesMetadataBuilder : IEntityBuilder<SeriesMetadata>
{
    private readonly SeriesMetadata _seriesMetadata;
    public SeriesMetadata Build() => _seriesMetadata;

    public SeriesMetadataBuilder()
    {
        _seriesMetadata = new SeriesMetadata()
        {
            CollectionTags = new List<CollectionTag>(),
            Genres = new List<Genre>(),
            Tags = new List<Tag>(),
            People = new List<SeriesMetadataPeople>()
        };
    }

    [Obsolete]
    public SeriesMetadataBuilder WithCollectionTag(CollectionTag tag)
    {
        _seriesMetadata.CollectionTags ??= new List<CollectionTag>();
        _seriesMetadata.CollectionTags.Add(tag);
        return this;
    }

    [Obsolete]
    public SeriesMetadataBuilder WithCollectionTags(IList<CollectionTag> tags)
    {
        if (tags == null) return this;
        _seriesMetadata.CollectionTags ??= new List<CollectionTag>();
        _seriesMetadata.CollectionTags = tags;
        return this;
    }

    public SeriesMetadataBuilder WithPublicationStatus(PublicationStatus status, bool lockState = false)
    {
        _seriesMetadata.PublicationStatus = status;
        _seriesMetadata.PublicationStatusLocked = lockState;
        return this;
    }

    public SeriesMetadataBuilder WithAgeRating(AgeRating rating, bool lockState = false)
    {
        _seriesMetadata.AgeRating = rating;
        _seriesMetadata.AgeRatingLocked = lockState;
        return this;
    }

    public SeriesMetadataBuilder WithPerson(Person person, PersonRole role)
    {
        _seriesMetadata.People ??= new List<SeriesMetadataPeople>();
        _seriesMetadata.People.Add(new SeriesMetadataPeople()
        {
            Role = role,
            Person = person,
            SeriesMetadata = _seriesMetadata,
        });
        return this;
    }

    public SeriesMetadataBuilder WithLanguage(string languageCode)
    {
        _seriesMetadata.Language = languageCode;
        return this;
    }

    public SeriesMetadataBuilder WithReleaseYear(int year, bool lockStatus = false)
    {
        _seriesMetadata.ReleaseYear = year;
        _seriesMetadata.ReleaseYearLocked = lockStatus;
        return this;
    }

    public SeriesMetadataBuilder WithSummary(string summary, bool lockStatus = false)
    {
        _seriesMetadata.Summary = summary;
        _seriesMetadata.SummaryLocked = lockStatus;
        return this;
    }

    public SeriesMetadataBuilder WithGenre(Genre genre, bool lockStatus = false)
    {
        _seriesMetadata.Genres ??= [];
        _seriesMetadata.Genres.Add(genre);
        _seriesMetadata.GenresLocked = lockStatus;
        return this;
    }

    public SeriesMetadataBuilder WithGenres(List<Genre> genres, bool lockStatus = false)
    {
        _seriesMetadata.Genres = genres;
        _seriesMetadata.GenresLocked = lockStatus;
        return this;
    }

    public SeriesMetadataBuilder WithTag(Tag tag, bool lockStatus = false)
    {
        _seriesMetadata.Tags ??= [];
        _seriesMetadata.Tags.Add(tag);
        _seriesMetadata.TagsLocked = lockStatus;
        return this;
    }
}
