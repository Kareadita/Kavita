using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kavita.Models.Scanner;

namespace Kavita.API.Services.Scanner;

/// <summary>
/// Decides when a detected folder change has finished arriving, so the folder scan does not read a half-copied archive and
/// record a Media Error for it. Sits between change detection (folder watching today, polling later) and ScanFolder
/// </summary>
public interface IFolderValidationService
{
    /// <summary>
    /// Hangfire job: queues ScanFolder once no new file under the change is still being copied in, or once the 10 minute cap
    /// since <paramref name="firstChangeUtc"/> has passed. Otherwise it schedules itself again after the quiet time
    /// </summary>
    /// <param name="request">The change's folder scan request</param>
    /// <param name="firstChangeUtc">When the first change was seen; carried across retries so the cap does not restart</param>
    Task ValidateFolder(ScanFolderRequest request, DateTime firstChangeUtc);

    /// <summary>
    /// Finds the files in a changed folder that are still being copied in
    /// </summary>
    /// <remarks>
    /// Only files Kavita reads (archives, epub, pdf, images) written since shortly before the first change are checked; older
    /// files are complete. A file counts as still being copied when another process holds it open for writing (Windows copies
    /// do until the last byte is in), or when the library Kavita reads it with cannot open it yet (a half-written archive has
    /// no end yet, which is the only signal on Linux, where an open never fails)
    /// </remarks>
    /// <param name="folder">The change's folder</param>
    /// <param name="recursive">False for a library root, where a recursive listing would walk the whole library</param>
    /// <param name="sinceUtc">When the first change was seen</param>
    IList<string> FindFilesStillWriting(string folder, bool recursive, DateTime sinceUtc);
}
