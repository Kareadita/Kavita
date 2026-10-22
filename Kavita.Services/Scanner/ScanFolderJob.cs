using System;
using Kavita.Models.Scanner;

namespace Kavita.Services.Scanner;

/// <param name="RunAtUtc">When a Scheduled job is due, null once it is queued to run</param>
/// <param name="FirstChangeUtc">Set on a watcher's validation job, null on a scan</param>
public sealed record ScanFolderJob(string JobId, ScanFolderRequest Request, DateTime? RunAtUtc, DateTime? FirstChangeUtc);
