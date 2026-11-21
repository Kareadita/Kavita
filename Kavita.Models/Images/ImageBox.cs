namespace Kavita.Models.Images;
#nullable enable

/// <summary>
/// The largest size an image of a kind is stored at
/// </summary>
public readonly record struct ImageBox(int Width, int Height);
