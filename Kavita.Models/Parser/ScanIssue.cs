using System;
using Kavita.Common.Extensions;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.Parser;
#nullable enable

/// <summary>
/// An issue with a file the scanner listed this scan, with the stamp from the listing
/// </summary>
/// <param name="Path">Normalized full path</param>
public sealed record ScanIssue(string Path, long Bytes, DateTime LastWriteTimeUtc, MediaErrorReason Reason,
    string Details)
{
    public static ScanIssue From(FileStamp stamp, ParseIssue issue) =>
        new(stamp.Path.NormalizePath(), stamp.Bytes, stamp.LastWriteTimeUtc, issue.Reason, issue.Details);
}
