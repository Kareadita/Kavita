using System;
using API.Entities.Metadata;

namespace API.Helpers.Builders;

public class ExternalSeriesMetadataBuilder : IEntityBuilder<ExternalSeriesMetadata>
{
    private readonly ExternalSeriesMetadata _metadata;
    public ExternalSeriesMetadata Build() => _metadata;

    public ExternalSeriesMetadataBuilder()
    {
        _metadata = new ExternalSeriesMetadata();
    }

    /// <summary>
    /// -1 for not set, Range 0 - 100
    /// </summary>
    /// <param name="rating"></param>
    /// <returns></returns>
    public ExternalSeriesMetadataBuilder WithAverageExternalRating(int rating)
    {
        _metadata.AverageExternalRating = Math.Clamp(rating, -1, 100);
        return this;
    }
}
