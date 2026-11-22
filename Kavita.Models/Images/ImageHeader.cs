namespace Kavita.Models.Images;
#nullable enable

/// <summary>
/// What an encoded image holds, read from its header rather than its file name
/// </summary>
/// <param name="Extension">With its dot (<c>.png</c>, <c>.jpg</c>, <c>.webp</c>, <c>.gif</c>, <c>.avif</c>). Null when
/// libvips reads the image but Kavita does not store that format, such as HEIC</param>
public sealed record ImageHeader(string? Extension, int Width, int Height);
