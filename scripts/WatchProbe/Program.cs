using System.Globalization;
using System.Text.Json;
using WatchProbe;

const string tool = "watchprobe";
const string probeVersion = "2";

const string usage = """
    Kavita folder watching probe. Shows which way of watching a folder sees changes on your setup.

    Usage:
      watchprobe <folder> [--minutes 10] [--interval 5] [--no-control]
      watchprobe cost <folder> [--runs 2] [--gap 15] [--no-full]      read-only, safe on a whole library

    Watch options:
      --minutes <n>    how long to watch (default 10). Enter or Ctrl+C ends it early
      --interval <s>   seconds between the poller's listings (default 5)
      --no-control     skip writing and deleting a test file in <folder> (use on a read-only mount)

    Cost options:
      --runs <n>       how many times to time each way of polling (default 2)
      --gap <s>        seconds to wait between timings, longer than the mount's attribute cache (default 15)
      --no-full        skip timing the whole-tree listing (the slowest one)

    Common options:
      --label <text>   name for this setup, e.g. "horo-docker-cifs"
      --out <file>     report path (default: next to this program)
    """;

if (args.Length < 1 || args[0] is "-h" or "--help")
{
    Console.WriteLine(usage);
    return 1;
}

var mode = args[0] == "cost" ? "cost" : "watch";
var rest = mode == "cost" ? args[1..] : args;
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
    var costReport = CostProbe.Run(environment, runs, gap, !options.ContainsKey("no-full"));
    var costPath = ProbeCli.SaveReport(tool, environment, options.GetValueOrDefault("out"),
        JsonSerializer.Serialize(costReport, WatchJsonContext.Default.CostReport));
    CostProbe.PrintSummary(costReport);
    Console.WriteLine($"Report written to {costPath}");
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
