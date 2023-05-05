using System.IO;
using API.Entities;

namespace API.Helpers.Builders;

public class MediaErrorBuilder : IEntityBuilder<MediaError>
{
    private readonly MediaError _mediaError;
    public MediaError Build() => _mediaError;

    public MediaErrorBuilder(string filePath)
    {
        _mediaError = new MediaError()
        {
            FilePath = filePath,
            Extension = Path.GetExtension(filePath).Replace(".", string.Empty).ToUpperInvariant()
        };
    }

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
