using System.Collections.Generic;
using System.Text;
using Kavita.Common;

namespace Kavita.Models.Misc;

/// <summary>
/// The outcome of running an external process
/// </summary>
public class ProcessResult
{
    /// <summary>
    /// How much of a failing process' output is repeated in the exception message
    /// </summary>
    private const int MaxReportedOutputLength = 2000;

    public required string Executable { get; init; }
    public required IReadOnlyList<string> Arguments { get; init; }
    public required int ExitCode { get; init; }
    public required string StandardOutput { get; init; }
    public required string StandardError { get; init; }

    public bool Succeeded => ExitCode == 0;

    public string CommandLine => $"{Executable} {string.Join(' ', Arguments)}";

    /// <summary>
    /// Throws an <see cref="ExecutionException"/> when the process exited non-zero, repeating the tail
    /// of its output so the failure can be diagnosed without digging through the log
    /// </summary>
    public void ThrowIfFailed()
    {
        if (Succeeded) return;

        var description = new StringBuilder($"'{CommandLine}' exited with code {ExitCode}.");

        AppendOutput(description, "stderr", StandardError);
        AppendOutput(description, "stdout", StandardOutput);

        throw new KavitaException(description.ToString());
    }

    private static void AppendOutput(StringBuilder description, string streamName, string output)
    {
        var trimmed = output.Trim();
        if (trimmed.Length == 0) return;

        // A failing build puts the interesting part last
        if (trimmed.Length > MaxReportedOutputLength)
        {
            trimmed = "..." + trimmed[^MaxReportedOutputLength..];
        }

        description.Append($" {streamName}: {trimmed}");
    }
}
