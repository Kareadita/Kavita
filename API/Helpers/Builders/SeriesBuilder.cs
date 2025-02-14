using System.Collections.Generic;
using System.Linq;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;

namespace API.Helpers.Builders;

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
            NormalizedLocalizedName = name.ToNormalized(),

            OriginalName = name,
            SortName = name,
            NormalizedName = name.ToNormalized(),
            Metadata = new SeriesMetadataBuilder()
                .WithPublicationStatus(PublicationStatus.OnGoing)
                .Build(),
            Volumes = new List<Volume>(),
            ExternalSeriesMetadata = new ExternalSeriesMetadata()
        };
    }

    /// <summary>
    /// Sets the localized name. If null or empty, defaults back to the
    /// </summary>
    /// <param name="localizedName"></param>
    /// <returns></returns>
    public SeriesBuilder WithLocalizedName(string localizedName, bool lockStatus = false)
    {
        // Why is this here?
        if (string.IsNullOrEmpty(localizedName))
        {
            localizedName = _series.Name;
        }

        _series.LocalizedName = localizedName;
        _series.NormalizedLocalizedName = localizedName.ToNormalized();
        _series.LocalizedNameLocked = lockStatus;
        return this;
    }

    public SeriesBuilder WithLocalizedNameAllowEmpty(string localizedName, bool lockStatus = false)
    {
        _series.LocalizedName = localizedName;
        _series.NormalizedLocalizedName = localizedName.ToNormalized();
        _series.LocalizedNameLocked = lockStatus;
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

    public SeriesBuilder WithCoverImage(string cover)
    {
        _series.CoverImage = cover;
        return this;
    }

    public SeriesBuilder WithLibraryId(int id)
    {
        _series.LibraryId = id;
        return this;
    }

    public SeriesBuilder WithPublicationStatus(PublicationStatus status)
    {
        _series.Metadata.PublicationStatus = status;
        return this;
    }

    public SeriesBuilder WithExternalMetadata(ExternalSeriesMetadata metadata)
    {
        _series.ExternalSeriesMetadata = metadata;
        return this;
    }


    public SeriesBuilder WithRelationship(int targetSeriesId, RelationKind kind)
    {
        _series.Relations ??= [];
        _series.Relations.Add(new SeriesRelation()
        {
            RelationKind = kind,
            TargetSeriesId = targetSeriesId
        });

        return this;
    }
}
