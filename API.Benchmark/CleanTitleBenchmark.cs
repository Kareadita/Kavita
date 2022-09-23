using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;

namespace API.Benchmark;

[MemoryDiagnoser]
public static class CleanTitleBenchmarks
{
    private static IList<string> _names;

    [GlobalSetup]
    public static void LoadData() => _names = File.ReadAllLines("Data/Comics.txt");

    [Benchmark]
    public static void TestCleanTitle()
    {
        foreach (var name in _names)
        {
            Services.Tasks.Scanner.Parser.Parser.CleanTitle(name, true);
        }
    }
}
