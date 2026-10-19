using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Misc;

namespace Kavita.API.Services;

public interface IProcessRunner
{
    /// <param name="executable"></param>
    /// <param name="arguments"></param>
    /// <param name="workingDirectory">Directory to run in, or null to inherit the current one</param>
    /// <param name="ct"></param>
    Task<ProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments, string? workingDirectory, CancellationToken ct);
}
