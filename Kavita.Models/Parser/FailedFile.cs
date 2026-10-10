using System;

namespace Kavita.Models.Parser;

/// <summary>
/// A file the scanner listed but could not turn into a series file, as it was on disk when it failed
/// </summary>
/// <param name="Path">Normalized full path</param>
public sealed record FailedFile(string Path, long Bytes, DateTime LastWriteTimeUtc);
