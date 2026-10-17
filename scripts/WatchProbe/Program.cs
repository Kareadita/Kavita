using System.Globalization;
using System.Text.Json;
using WatchProbe;

const string tool = "watchprobe";
const string probeVersion = "4";

const string usage = """
    Kavita folder watching probe. Shows which way of watching a folder sees changes on your setup.

    Usage:
      watchprobe <folder> [--minutes 10] [--interval 5] [--no-control]
      watchprobe cost <folder> [--runs 2] [--gap 15] [--methods a,b] [--no-full]      read-only, safe on a whole library
      watchprobe copy <folder> [--source <file>] [--size-mb 300] [--seconds 60] [--methods a,b]
                                    writes and deletes watchprobe-copy-* folders in <folder>

    Watch options:
      --minutes <n>    how long to watch (default 10). Enter or Ctrl+C ends it early
      --interval <s>   seconds between the poller's listings (default 5)
      --no-control     skip writing and deleting a test file in <folder> (use on a read-only mount)

    Cost options:
      --runs <n>       how many times to time each way of polling (default 2)
      --gap <s>        seconds to wait between timings, longer than the mount's attribute cache (default 15)
      --methods <list> which ways to time, comma-separated: folder-names, folder-times, known-folders, full (default all)
      --no-full        skip timing the whole-tree listing (the slowest one)

    Copy options:
      --source <file>  file to copy in (default: a random file of --size-mb in the temp folder)
      --size-mb <n>    size of the generated source (default 300)
      --seconds <s>    how long the throttled copies (stream, robocopy) take (default 60)
      --methods <list> which ways to copy: stream, file-copy, robocopy, shell (default all; robocopy and shell on Windows only)

    Common options:
      --label <text>   name for this setup, e.g. "horo-docker-cifs"
      --out <file>     report path (default: next to this program)
    """;

if (args.Length < 1 || args[0] is "-h" or "--help")
{
    Console.WriteLine(usage);
    return 1;
}

var mode = args[0] is "cost" or "copy" ? args[0] : "watch";
var rest = mode == "watch" ? args : args[1..];
if (rest.Length < 1)
{
    Console.WriteLine(usage);
    return 1;
}

var folder = rest[0];
var options = ProbeCli.ParseOptions(rest[1..]);

if (!Directory.Exists(folder))
{
    Console.Error.WriteLine($"Folder not found: {folder}");
    return 1;
}

var environment = EnvironmentReader.Read(probeVersion, mode, folder, options.GetValueOrDefault("label"));
ProbeCli.PrintEnvironment(tool, environment);

if (mode == "cost")
{
    var runs = int.Parse(options.GetValueOrDefault("runs") ?? "2");
    var gap = TimeSpan.FromSeconds(int.Parse(options.GetValueOrDefault("gap") ?? "15"));
    var methods = options.GetValueOrDefault("methods") is { } list
        ? list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
        : CostProbe.AllMethods.ToList();
    if (options.ContainsKey("no-full"))
    {
        methods.Remove("full");
    }
    var unknown = methods.Except(CostProbe.AllMethods).ToList();
    if (unknown.Count > 0 || methods.Count == 0)
    {
        Console.Error.WriteLine($"Unknown or no --methods: {string.Join(", ", unknown)}. Pick from {string.Join(", ", CostProbe.AllMethods)}");
        return 1;
    }

    var costReport = CostProbe.Run(environment, runs, gap, methods);
    var costPath = ProbeCli.SaveReport(tool, environment, options.GetValueOrDefault("out"),
        JsonSerializer.Serialize(costReport, WatchJsonContext.Default.CostReport));
    CostProbe.PrintSummary(costReport);
    Console.WriteLine($"Report written to {costPath}");
    return 0;
}

if (mode == "copy")
{
    var requested = options.GetValueOrDefault("methods") is { } list
        ? list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
        : CopyProbe.AllMethods.ToList();
    var unknown = requested.Except(CopyProbe.AllMethods).ToList();
    if (unknown.Count > 0)
    {
        Console.Error.WriteLine($"Unknown --methods: {string.Join(", ", unknown)}. Pick from {string.Join(", ", CopyProbe.AllMethods)}");
        return 1;
    }

    var source = options.GetValueOrDefault("source")
                 ?? CopyProbe.CreateSource(int.Parse(options.GetValueOrDefault("size-mb") ?? "300"));
    if (!File.Exists(source))
    {
        Console.Error.WriteLine($"Source not found: {source}");
        return 1;
    }

    var seconds = double.Parse(options.GetValueOrDefault("seconds") ?? "60", CultureInfo.InvariantCulture);
    var copyReport = CopyProbe.Run(environment, source, seconds, CopyProbe.MethodsFor(requested));
    var copyPath = ProbeCli.SaveReport(tool, environment, options.GetValueOrDefault("out"),
        JsonSerializer.Serialize(copyReport, WatchJsonContext.Default.CopyReport));
    CopyProbe.PrintSummary(copyReport);
    Console.WriteLine($"Report written to {copyPath}");
    return 0;
}

var minutes = double.Parse(options.GetValueOrDefault("minutes") ?? "10", CultureInfo.InvariantCulture);
var interval = TimeSpan.FromSeconds(int.Parse(options.GetValueOrDefault("interval") ?? "5"));

if (InotifyLimits.Read() is { } inotify)
{
    Console.WriteLine($"  inotify: max_user_watches {inotify.MaxUserWatches}, max_user_instances {inotify.MaxUserInstances}");
}

var outPath = ProbeCli.ReportPath(tool, environment, options.GetValueOrDefault("out"));
var json = WatchJsonContext.Default.WatchReport;
var session = new WatchSession(environment, interval, !options.ContainsKey("no-control"));

using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(minutes));
var endReason = "time";
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    endReason = "ctrl+c";
    cts.Cancel();
};
if (!Console.IsInputRedirected)
{
    _ = Task.Run(() =>
    {
        if (Console.ReadLine() == null) return;
        endReason = "enter";
        cts.Cancel();
    });
}

Console.WriteLine($"Watching for {minutes:0.#} minutes. Press Enter or Ctrl+C to finish early, the report is written either way.");
Console.WriteLine($"The report so far is saved every 30 s to {outPath}");
Console.WriteLine();

await session.RunAsync(() => File.WriteAllText(outPath, JsonSerializer.Serialize(session.BuildReport("running", minutes), json)), cts.Token);

var report = session.BuildReport(endReason, minutes);
File.WriteAllText(outPath, JsonSerializer.Serialize(report, json));
PrintSummary(report);
Console.WriteLine($"Report written to {outPath}");
return 0;

static void PrintSummary(WatchReport report)
{
    Console.WriteLine();
    Console.WriteLine("Summary");
    foreach (var source in report.Sources)
    {
        var state = source.Started ? $"{source.Events} events" : $"did not start: {source.Error}";
        Console.WriteLine($"  {source.Name,-21} {state}");
    }

    if (report.Control is { } control)
    {
        if (control.Error != null)
        {
            Console.WriteLine($"  control: {control.Error}");
        }
        foreach (var step in control.Steps)
        {
            Console.WriteLine($"  control {step.Action,-6}  watcher {Seconds(step.Watcher)}, provider {Seconds(step.Provider)}, poller {Seconds(step.Poller)}");
        }
    }

    var p = report.Poller;
    if (p.Polls > 0)
    {
        Console.WriteLine($"  poller: {p.Polls} listings, {p.MinMs:F0} / {p.MedianMs:F0} / {p.MaxMs:F0} ms (min / median / max), {p.Errors} errors");
    }

    var files = report.Changes.Where(c => !c.IsFolder && (report.Control == null || c.Path != report.Control.File)).ToList();
    Console.WriteLine($"  poller found {files.Count} file changes besides the control: watcher saw {files.Count(c => c.WatcherSeconds != null)}, " +
                      $"provider fired near {files.Count(c => c.ProviderFired)}, folder times would have caught {files.Count(c => c.ParentFolderSeconds != null)}");
}

static string Seconds(double? value) => value is { } v ? $"{v:F1} s" : "missed";
