namespace Kavita.Models.Images;

/// <summary>
/// What the image pipeline wrote
/// </summary>
/// <param name="FileName">File name with extension, without the directory</param>
/// <param name="Width">Width of the written file</param>
/// <param name="Height">Height of the written file</param>
/// <param name="SourceWidth">Width of the source before sizing</param>
/// <param name="SourceHeight">Height of the source before sizing</param>
public sealed record ImageWriteResult(string FileName, int Width, int Height, int SourceWidth, int SourceHeight);
