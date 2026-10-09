using System;
using System.Collections.Generic;

namespace Kavita.Services.Scanner;

/// <param name="Method">The <see cref="TaskScheduler"/> method that was delayed: ScanLibraries, ScanLibrary or ScanSeries</param>
public sealed record DelayedScanJob(string JobId, string Method, IReadOnlyList<object?> Args, DateTime CreatedAtUtc, DateTime RunAtUtc);
