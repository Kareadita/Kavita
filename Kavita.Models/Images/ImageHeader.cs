namespace Kavita.Models.Images;
#nullable enable

/// <summary>
/// What an encoded image holds, read from its header rather than its file name
/// </summary>
/// <param name="Extension">With its dot, as <c>ImageHelper.GetImageExtension</c> returns it</param>
public sealed record ImageHeader(string Extension, int Width, int Height);
