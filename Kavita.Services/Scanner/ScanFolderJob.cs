using System;
using Kavita.Models.Scanner;

namespace Kavita.Services.Scanner;

/// <param name="RunAtUtc">When a Scheduled job is due, null once it is queued to run</param>
public sealed record ScanFolderJob(string JobId, ScanFolderRequest Request, DateTime? RunAtUtc);
