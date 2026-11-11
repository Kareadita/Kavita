using System;

namespace Kavita.Models.Images;

public abstract record ImageSource
{
    public sealed record FromFile(string Path) : ImageSource;

    public sealed record FromBytes(ReadOnlyMemory<byte> Data) : ImageSource;

    public sealed record FromStream(Func<System.IO.Stream> Open) : ImageSource;
}
