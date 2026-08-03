using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Services;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Kobo;

/// <summary>
/// Invokes kepubify as <c>kepubify -o output.kepub.epub input.epub</c> (CW-compatible flags).
/// </summary>
public class KepubifyRunner(ILogger<KepubifyRunner> logger) : IKepubifyRunner
{
    public async Task ConvertAsync(string kepubifyBinaryPath, string inputEpubPath, string outputKepubPath,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kepubifyBinaryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputEpubPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputKepubPath);

        if (!File.Exists(inputEpubPath))
        {
            throw new FileNotFoundException("KEPUB source EPUB was not found", inputEpubPath);
        }

        var outputDir = Path.GetDirectoryName(outputKepubPath);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = kepubifyBinaryPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        // Explicit -o file path so output lands at the fingerprint cache location.
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(outputKepubPath);
        startInfo.ArgumentList.Add(inputEpubPath);

        using var process = new Process { StartInfo = startInfo };
        logger.LogDebug("Running kepubify for {Input} → {Output}", inputEpubPath, outputKepubPath);

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start kepubify at '{kepubifyBinaryPath}'");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not kill cancelled kepubify process");
            }

            throw;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (process.ExitCode != 0)
        {
            logger.LogWarning(
                "kepubify exited {ExitCode} for {Input}. stderr={Stderr} stdout={Stdout}",
                process.ExitCode, inputEpubPath, stderr, stdout);
            throw new InvalidOperationException(
                $"kepubify failed with exit code {process.ExitCode}: {stderr}");
        }

        if (!File.Exists(outputKepubPath))
        {
            throw new InvalidOperationException(
                $"kepubify reported success but output was missing: {outputKepubPath}");
        }
    }
}
