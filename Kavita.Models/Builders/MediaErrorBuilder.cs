using System.IO;
using Kavita.Common.Extensions;
using Kavita.Models.Entities;

namespace Kavita.Models.Builders;

public class MediaErrorBuilder(string filePath): IEntityBuilder<MediaError>
{
    private readonly MediaError _mediaError = new()
    {
        FilePath = filePath.NormalizePath(),
        Extension = Path.GetExtension(filePath).Replace(".", string.Empty).ToUpperInvariant()
    };

    public MediaError Build() => _mediaError;

    public MediaErrorBuilder WithComment(string comment)
    {
        _mediaError.Comment = comment.Trim();
        return this;
    }

    public MediaErrorBuilder WithDetails(string details)
    {
        _mediaError.Details = details.Trim();
        return this;
    }
}
