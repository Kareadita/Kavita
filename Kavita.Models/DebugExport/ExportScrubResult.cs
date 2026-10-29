using System.Collections.Generic;

namespace Kavita.Models.DebugExport;

/// <param name="Junk">Handed on to the log and system-info steps, so a value they find gets the replacement the database gave it</param>
/// <param name="Usernames">Original username to <c>user-&lt;Id&gt;</c>, including MAL and scrobble provider usernames</param>
/// <param name="AdminAuthKey">Logs in even when the exported server disables password login</param>
public sealed record ExportScrubResult(
    ExportJunk Junk,
    IReadOnlyDictionary<string, string> Usernames,
    string AdminUserName,
    string AdminPassword,
    string AdminAuthKey);
