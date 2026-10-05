using System;

namespace Kavita.Models.Parser;

/// <summary>
/// A file as the database last saw it
/// </summary>
/// <param name="Path">Normalized full path</param>
/// <param name="LastWriteTimeUtc">Null for rows not read since the column was added</param>
public sealed record KnownFile(int Id, string Path, long Bytes, DateTime? LastWriteTimeUtc);
