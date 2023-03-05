using System.Collections.Generic;
using System.Linq;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;

namespace API.Tests.Helpers.Builders;

public class SeriesBuilder : IEntityBuilder<Series>
{
    private readonly Series _series;
    public Series Build()
    {
        _series.Pages = _series.Volumes.Sum(v => v.Chapters.Sum(c => c.Pages));
        return _series;
    }

    public SeriesBuilder(string name)
    {
        _series = new Series()
        {
            Name = name,
            LocalizedName = name.ToNormalized(),
            OriginalName = name,
            SortName = name,
            NormalizedName = name.ToNormalized(),
            NormalizedLocalizedName = name.ToNormalized(),
            Metadata = new SeriesMetadata(),
            Volumes = new List<Volume>()
        };
    }

    public SeriesBuilder WithLocalizedName(string localizedName)
    {
        _series.LocalizedName = localizedName;
        _series.NormalizedLocalizedName = localizedName.ToNormalized();
        return this;
    }

    public SeriesBuilder WithFormat(MangaFormat format)
    {
        _series.Format = format;
        return this;
    }

    public SeriesBuilder WithVolume(Volume volume)
    {
        _series.Volumes ??= new List<Volume>();
        _series.Volumes.Add(volume);
        return this;
    }

    public SeriesBuilder WithVolumes(List<Volume> volumes)
    {
        _series.Volumes = volumes;
        return this;
    }

    public SeriesBuilder WithMetadata(SeriesMetadata metadata)
    {
        _series.Metadata = metadata;
        return this;
    }

    public SeriesBuilder WithPages(int pages)
    {
        _series.Pages = pages;
        return this;
    }
}
