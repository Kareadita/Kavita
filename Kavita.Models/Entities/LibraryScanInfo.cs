using System;

namespace Kavita.Models.Entities;
#nullable enable

/// <summary>
/// The last library scan of each kind, stored as JSON on <see cref="Library.ScanInfo"/>
/// </summary>
public sealed record LibraryScanInfo
{
    public ScanRun? Normal { get; init; }
    public ScanRun? Forced { get; init; }

    public sealed record ScanRun
    {
        public DateTime AtUtc { get; init; }
        /// <summary>
        /// From scan start to the library save; series removal and issue reporting run after it
        /// </summary>
        public long DurationMs { get; init; }
        /// <summary>
        /// Folders below the library roots walked by the scan, 0 when the library falls back to a series scan
        /// </summary>
        public int WalkedFolderCount { get; init; }
        public int FilesParsed { get; init; }
        public long ListMs { get; init; }
        public long WalkMs { get; init; }
        public long ParseMs { get; init; }
    }
}
