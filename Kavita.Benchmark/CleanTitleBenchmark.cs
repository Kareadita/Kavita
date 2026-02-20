using BenchmarkDotNet.Attributes;
using Kavita.Services.Scanner;

namespace Kavita.Benchmark;

[MemoryDiagnoser]
public class CleanTitleBenchmarks
{
    private static IList<string> _names;

    [GlobalSetup]
    public static void LoadData() => _names = File.ReadAllLines("Data/Comics.txt");

    [Benchmark]
    public static void TestCleanTitle()
    {
        foreach (var name in _names)
        {
            Parser.CleanTitle(name, true);
        }
    }
}
