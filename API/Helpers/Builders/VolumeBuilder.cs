using System;
using System.Collections.Generic;
using System.Linq;
using API.Data;
using API.Entities;

namespace API.Helpers.Builders;

public class VolumeBuilder : IEntityBuilder<Volume>
{
    private readonly Volume _volume;
    public Volume Build() => _volume;

    public VolumeBuilder(string volumeNumber)
    {
        _volume = new Volume()
        {
            Name = volumeNumber,
            LookupName = volumeNumber,
            MinNumber = Services.Tasks.Scanner.Parser.Parser.MinNumberFromRange(volumeNumber),
            MaxNumber = Services.Tasks.Scanner.Parser.Parser.MaxNumberFromRange(volumeNumber),
            Chapters = new List<Chapter>()
        };
    }

    public VolumeBuilder WithName(string name)
    {
        _volume.Name = name;
        return this;
    }

    public VolumeBuilder WithNumber(float number)
    {
        _volume.MinNumber = number;
        if (_volume.MaxNumber < number)
        {
            _volume.MaxNumber = number;
        }
        return this;
    }

    public VolumeBuilder WithMinNumber(float number)
    {
        _volume.MinNumber = number;
        return this;
    }

    public VolumeBuilder WithMaxNumber(float number)
    {
        _volume.MaxNumber = number;
        return this;
    }

    public VolumeBuilder WithChapters(IList<Chapter> chapters)
    {
        _volume.Chapters = chapters;
        return this;
    }

    public VolumeBuilder WithChapter(Chapter chapter)
    {
        _volume.Chapters ??= new List<Chapter>();
        _volume.Chapters.Add(chapter);
        _volume.Pages = _volume.Chapters.Sum(c => c.Pages);
        return this;
    }

    public VolumeBuilder WithSeriesId(int seriesId)
    {
        _volume.SeriesId = seriesId;
        return this;
    }

    public VolumeBuilder WithCoverImage(string cover)
    {
        _volume.CoverImage = cover;
        return this;
    }

    public VolumeBuilder WithCreated(DateTime created)
    {
        _volume.Created = created;
        _volume.CreatedUtc = created.ToUniversalTime();
        return this;
    }

    public VolumeBuilder WithLastModified(DateTime lastModified)
    {
        _volume.LastModified = lastModified;
        _volume.LastModifiedUtc = lastModified.ToUniversalTime();
        return this;
    }
}
