using System;
using Kavita.API.Services;
using Kavita.Models.Images;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Images;

public class ImagePipeline(ILogger<ImagePipeline> logger) : IImagePipeline
{
    public ImageWriteResult Write(ImageSource source, ImageKind kind, ImagePolicy policy, string directory, string fileName)
    {
        // TODO: Implement
        throw new NotImplementedException();
    }
}
