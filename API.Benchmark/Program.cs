using BenchmarkDotNet.Running;

namespace API.Benchmark;

/// <summary>
/// To build this, cd into API.Benchmark directory and run
/// dotnet build -c Release
/// then copy the outputted dll
/// dotnet copied_string\API.Benchmark.dll
/// </summary>
public static class Program
{
    private static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
