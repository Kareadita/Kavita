using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Extensions;

namespace Kavita.Benchmark;

/// <summary>
/// The cost of matching one folder's files against the library's extension pattern, the way
/// DirectoryService.GetFilesWithCertainExtensions does it once per folder during a scan
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class FileExtensionRegexBenchmark
{
    private static readonly string Pattern = string.Join("|",
        new[] { FileTypeGroup.Archive, FileTypeGroup.Epub, FileTypeGroup.Pdf, FileTypeGroup.Images }.Select(g => g.GetRegex()));

    private static readonly Regex Cached = new(Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

    private string[] _extensions = [];

    [Params(5, 150)]
    public int FilesInFolder { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var pool = new[] { ".cbz", ".CBR", ".epub", ".pdf", ".jpg", ".txt", ".xml" };
        _extensions = Enumerable.Range(0, FilesInFolder).Select(i => pool[i % pool.Length]).ToArray();
    }

    [Benchmark(Baseline = true)]
    public int NewCompiledPerFolder()
    {
        var regex = new Regex(Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));
        return Count(regex);
    }

    [Benchmark]
    public int NewInterpretedPerFolder()
    {
        var regex = new Regex(Pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500));
        return Count(regex);
    }

    [Benchmark]
    public int CachedCompiled() => Count(Cached);

    private int Count(Regex regex)
    {
        var count = 0;
        foreach (var extension in _extensions)
        {
            if (regex.IsMatch(extension)) count++;
        }
        return count;
    }
}
