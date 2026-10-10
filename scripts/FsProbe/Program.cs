using System.Text.Json;
using FsProbe;

const string usage = """
    Kavita filesystem probe. Collects how a library folder reports file and folder times.

    Usage:
      fsprobe passive <folder> [--runs 2] [--no-ctime]     read-only, safe on a real library
      fsprobe active  <folder> [--delays 0,5,65] [--keep]  writes a temp folder inside <folder>, then deletes it
      fsprobe mark    <folder>                             save a snapshot (pair with check)
      fsprobe check   <folder> --snapshot <file>           compare with a snapshot from mark

    Common options:
      --label <text>   name for this setup, e.g. "unraid-user-share"
      --out <file>     report path (default: next to this program)
    """;

if (args.Length < 2 || args[0] is "-h" or "--help")
{
    Console.WriteLine(usage);
    return 1;
}

var mode = args[0].ToLowerInvariant();
var folder = args[1];
var options = ParseOptions(args.Skip(2).ToArray());

if (!Directory.Exists(folder))
{
    Console.Error.WriteLine($"Folder not found: {folder}");
    return 1;
}

var label = options.GetValueOrDefault("label");
var environment = EnvironmentReader.Read(mode, folder, label);
Console.WriteLine($"fsprobe v{EnvironmentReader.ProbeVersion} {mode} on {environment.Path}");
Console.WriteLine($"  {environment.Os} | {environment.Rid} | container: {environment.InContainer}");
Console.WriteLine($"  filesystem: {environment.FileSystem ?? "?"} {environment.DriveType} | mount: {environment.MountPoint} {environment.MountOptions}");

var json = ProbeJsonContext.Default;
string outPath;
switch (mode)
{
    case "passive":
    {
        var runs = int.Parse(options.GetValueOrDefault("runs") ?? "2");
        var report = PassiveProbe.Run(environment.Path, environment, runs, !options.ContainsKey("no-ctime"));
        PrintPassive(report);
        outPath = Save(JsonSerializer.Serialize(report, json.PassiveReport));
        break;
    }
    case "active":
    {
        var delays = (options.GetValueOrDefault("delays") ?? "0,5,65").Split(',').Select(int.Parse).ToList();
        var report = ActiveProbe.Run(environment.Path, environment, delays, options.ContainsKey("keep"));
        outPath = Save(JsonSerializer.Serialize(report, json.ActiveReport));
        break;
    }
    case "mark":
    {
        var snapshot = SnapshotProbe.Mark(environment.Path, environment);
        outPath = Save(JsonSerializer.Serialize(snapshot, json.Snapshot));
        Console.WriteLine($"  next: make a change, then run: fsprobe check \"{folder}\" --snapshot \"{outPath}\"");
        break;
    }
    case "check":
    {
        var snapshotPath = options.GetValueOrDefault("snapshot");
        if (snapshotPath == null)
        {
            Console.Error.WriteLine("check needs --snapshot <file from mark>");
            return 1;
        }
        var snapshot = JsonSerializer.Deserialize(File.ReadAllText(snapshotPath), json.Snapshot)!;
        var report = SnapshotProbe.Check(environment.Path, snapshot, environment);
        outPath = Save(JsonSerializer.Serialize(report, json.CheckReport));
        break;
    }
    default:
        Console.WriteLine(usage);
        return 1;
}

Console.WriteLine($"Report written to {outPath}");
return 0;

string Save(string content)
{
    var path = options.GetValueOrDefault("out")
               ?? Path.Combine(AppContext.BaseDirectory,
                   $"fsprobe-{mode}-{Sanitize(label ?? Environment.MachineName)}-{DateTime.Now:yyyyMMdd-HHmmss}.json");
    File.WriteAllText(path, content);
    return path;
}

static string Sanitize(string value) =>
    string.Concat(value.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-'));

static Dictionary<string, string?> ParseOptions(string[] rest)
{
    var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < rest.Length; i++)
    {
        if (!rest[i].StartsWith("--")) continue;
        var key = rest[i][2..];
        var hasValue = i + 1 < rest.Length && !rest[i + 1].StartsWith("--");
        result[key] = hasValue ? rest[++i] : null;
    }
    return result;
}

static void PrintPassive(PassiveReport report)
{
    var p = report.Parity;
    Console.WriteLine($"  {report.Files} files, {report.Folders} folders");
    Console.WriteLine($"  listing vs per-file time: {p.MismatchRaw} differ raw, {p.MismatchTruncated} differ at 1 s " +
                      $"({p.FoldersAffected} folders), max delta {p.MaxDeltaMs:F1} ms");
    Console.WriteLine($"  root result same at 1 s: {report.RootResultsMatchTruncated}");
    if (report.ChangeTime is { } c)
    {
        Console.WriteLine($"  change time read {c.Read}, failed {c.Failed}, ctime later than mtime on {c.CtimeAfterMtime} files");
    }
    if (report.FutureMtime + report.MtimeBefore1990 > 0)
    {
        Console.WriteLine($"  odd mtimes: {report.FutureMtime} in the future, {report.MtimeBefore1990} before 1990");
    }
}
