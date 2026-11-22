namespace Kavita.Models.Images;
#nullable enable

/// <summary>
/// What the image pipeline rendered in memory
/// </summary>
/// <param name="Data">The encoded image</param>
/// <param name="Width">Width of the encoded image, after every step ran</param>
/// <param name="Height">Height of the encoded image, after every step ran</param>
public sealed record ImageRenderResult(byte[] Data, int Width, int Height);
