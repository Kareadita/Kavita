using System;
using System.IO;
using Kavita.Common.Extensions;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.Builders;

public class MediaErrorBuilder(string filePath): IEntityBuilder<MediaError>
{
    private readonly MediaError _mediaError = new()
    {
        FilePath = filePath.NormalizePath(),
        Extension = Path.GetExtension(filePath).Replace(".", string.Empty).ToUpperInvariant(),
        LastSeenUtc = DateTime.UtcNow,
    };

    public MediaError Build() => _mediaError;

    public MediaErrorBuilder WithProducer(MediaErrorProducer producer)
    {
        _mediaError.Producer = producer;
        return this;
    }

    public MediaErrorBuilder WithReason(MediaErrorReason reason)
    {
        _mediaError.Reason = reason;
        return this;
    }

    public MediaErrorBuilder WithDetails(string details)
    {
        _mediaError.Details = details.Trim();
        return this;
    }
}
