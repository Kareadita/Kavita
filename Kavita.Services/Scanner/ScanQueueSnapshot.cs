using System.Collections.Generic;
using Kavita.Models.Scanner;

namespace Kavita.Services.Scanner;

/// <summary>
/// The scan jobs Hangfire held at one moment, from <see cref="ScanJobQueue.Read"/>
/// </summary>
/// <param name="FolderRequests">ScanFolder jobs that have not started</param>
public sealed record ScanQueueSnapshot(IReadOnlyList<ScanJob> Jobs, IReadOnlyList<ScanFolderRequest> FolderRequests);
