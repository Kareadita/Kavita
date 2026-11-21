using System;
using System.Collections.Generic;

namespace Kavita.Models.Images;

#nullable enable

/// <summary>
/// A file's cover image, still open in its archive, epub or PDF. Write <see cref="Source"/> before disposing
/// </summary>
/// <remarks>The image is read from the open entry, so nothing is copied into memory first</remarks>
public sealed class OpenedCover(ImageSource source, params IDisposable[] owners) : IDisposable
{
    private readonly IReadOnlyList<IDisposable> _owners = owners;

    public ImageSource Source { get; } = source;

    public void Dispose()
    {
        if (Source is ImageSource.FromStream fromStream)
        {
            fromStream.Stream.Dispose();
        }

        for (var i = _owners.Count - 1; i >= 0; i--)
        {
            _owners[i].Dispose();
        }
    }
}
