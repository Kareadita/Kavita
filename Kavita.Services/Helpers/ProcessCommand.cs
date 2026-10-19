using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Models.Misc;

namespace Kavita.Services.Helpers;

/// <summary>
/// Runs a single external process
/// </summary>
public class ProcessCommand
{
    private readonly IProcessRunner _runner;
    private readonly string _executable;
    private readonly IReadOnlyList<string> _arguments;
    private readonly string? _workingDirectory;
    private readonly TimeSpan _timeout;

    private ProcessCommand(IProcessRunner runner, string executable, IReadOnlyList<string> arguments, string? workingDirectory, TimeSpan timeout)
    {
        _runner = runner;
        _executable = executable;
        _arguments = arguments;
        _workingDirectory = workingDirectory;
        _timeout = timeout;
    }

    public async Task<ProcessResult> ExecuteAsync(CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);

        return await _runner.RunAsync(_executable, _arguments, _workingDirectory, cts.Token);
    }

    public class Builder(IProcessRunner runner)
    {
        private string _executable = string.Empty;
        private List<string> _arguments = [];
        private string? _workingDirectory;
        private TimeSpan _timeout = TimeSpan.FromSeconds(30);

        public Builder WithExecutable(string executable)
        {
            _executable = executable;
            return this;
        }

        public Builder WithArguments(params string[] arguments)
        {
            _arguments = [.. arguments];
            return this;
        }

        public Builder AppendArgument(string argument)
        {
            _arguments.Add(argument);
            return this;
        }

        public Builder WithRepeatedArgument(string argument, string[] values)
        {
            foreach (var value in values)
            {
                _arguments.Add(argument);
                _arguments.Add(value);
            }

            return this;
        }

        public Builder AppendArgumentIf(bool condition, params string[] argument)
        {
            if (condition)
                _arguments.AddRange(argument);

            return this;
        }

        public Builder WithWorkingDirectory(string? workingDirectory)
        {
            _workingDirectory = workingDirectory;
            return this;
        }

        public Builder WithTimeout(TimeSpan timeout)
        {
            _timeout = timeout;
            return this;
        }

        public ProcessCommand Build()
        {
            if (string.IsNullOrWhiteSpace(_executable))
            {
                throw new ArgumentException("Executable cannot be null or empty");
            }

            return new ProcessCommand(runner, _executable, _arguments, _workingDirectory, _timeout);
        }
    }
}
