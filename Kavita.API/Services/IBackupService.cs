using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Kavita.API.Services;

public interface IBackupService
{
    public const string LogFile = "config/logs/kavita.log";

    Task BackupDatabase(CancellationToken ct = default);
    /// <summary>
    /// Returns a list of all log files for Kavita
    /// </summary>
    /// <param name="rollFiles">If file rolling is enabled. Defaults to True.</param>
    /// <returns></returns>
    IEnumerable<string> GetLogFiles(bool rollFiles = true);
    /// <summary>
    /// Writes a consistent copy of the live database to <paramref name="path"/> with <c>VACUUM INTO</c>, while it stays in use
    /// </summary>
    /// <exception cref="System.ArgumentException">The path holds a single quote</exception>
    Task SnapshotDatabase(string path, CancellationToken ct = default);
}
