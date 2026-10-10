using System;

namespace Kavita.Models.Parser;

/// <param name="Path">Full path as the listing returned it, not normalized</param>
public sealed record FileStamp(string Path, long Bytes, DateTime LastWriteTimeUtc);
