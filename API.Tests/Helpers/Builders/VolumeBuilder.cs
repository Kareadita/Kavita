using System.Collections.Generic;
using System.Linq;
using API.Data;
using API.Entities;

namespace API.Tests.Helpers.Builders;

public class VolumeBuilder : IEntityBuilder<Volume>
{
    private readonly Volume _volume;
    public Volume Build() => _volume;

    public VolumeBuilder(string volumeNumber)
    {
        _volume = DbFactory.Volume(volumeNumber);
    }

    public VolumeBuilder WithName(string name)
    {
        _volume.Name = name;
        return this;
    }

    public VolumeBuilder WithNumber(int number)
    {
        _volume.Number = number;
        return this;
    }

    public VolumeBuilder WithChapters(List<Chapter> chapters)
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
}
