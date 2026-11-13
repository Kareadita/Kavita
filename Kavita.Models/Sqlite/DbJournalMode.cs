using System.ComponentModel;

namespace Kavita.Models.Sqlite;
#nullable enable

/// <summary>
/// The SQLite journal modes <c>PRAGMA journal_mode</c> can report
/// </summary>
public enum DbJournalMode
{
    [Description("Delete")]
    Delete = 0,
    [Description("Truncate")]
    Truncate = 1,
    [Description("Persist")]
    Persist = 2,
    [Description("Memory")]
    Memory = 3,
    [Description("WAL")]
    Wal = 4,
    [Description("Off")]
    Off = 5,
}
