using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Models.Misc;
using Microsoft.Extensions.Logging;

namespace Kavita.Services;

/// <summary>
/// <see cref="IProcessRunner"/> backed by <see cref="System.Diagnostics.Process"/>
/// </summary>
/// <remarks>
/// Output is streamed to the log as it is produced, so a long build is still visible live, and
/// captured so a failure can be reported with the lines that explain it
/// </remarks>
public class ProcessRunner(ILogger<ProcessRunner> logger, IFileSystem fileSystem): IProcessRunner
{

    private static readonly string[] PathExtensions =
        Environment.GetEnvironmentVariable("PATHEXT")?.Split(';', StringSplitOptions.RemoveEmptyEntries)
        ?? [".EXE", ".CMD", ".BAT"];
    private static readonly string[] PathDirectories =
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

    public async Task<ProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments,
        string? workingDirectory, CancellationToken ct)
    {
        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();

        using var process = new Process();
        process.StartInfo.FileName = ResolveExecutable(executable);
        process.StartInfo.WorkingDirectory = workingDirectory ?? string.Empty;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
        process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
        process.StartInfo.UseShellExecute = false;

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is null) return;

            standardOutput.AppendLine(args.Data);
            logger.LogTrace("{Line}", args.Data);
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is null) return;

            standardError.AppendLine(args.Data);
            logger.LogTrace("{Line}", args.Data);
        };

        logger.LogDebug("Running {Executable} {Arguments} in {WorkingDirectory}", executable, string.Join(' ', arguments),
            workingDirectory ?? "(current directory)");

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(ct);

            // WaitForExitAsync returns before the output handlers have drained
            process.WaitForExit();
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return new ProcessResult
        {
            Executable = executable,
            Arguments = arguments,
            ExitCode = process.ExitCode,
            StandardOutput = standardOutput.ToString(),
            StandardError = standardError.ToString(),
        };
    }

    private string ResolveExecutable(string executable)
    {
        if (!OperatingSystem.IsWindows() || Path.HasExtension(executable)) return executable;

        if (fileSystem.File.Exists(executable)) return executable;

        foreach (var dir in PathDirectories)
        {
            foreach (var ext in PathExtensions)
            {
                var candidate = Path.Combine(dir, executable + ext);
                if (fileSystem.File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return executable;
    }
}
