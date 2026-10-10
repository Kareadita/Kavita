using System.Linq;
using Hangfire;
using Hangfire.Common;
using Kavita.Services.Scanner;

namespace Kavita.Services.Tests;

public class ScannerServiceRetryTests
{
    // A retry waiting in Scheduled does not count as busy, so it would run beside the scan started in the meantime
    [Theory]
    [InlineData(nameof(ScannerService.ScanSeries))]
    [InlineData(nameof(ScannerService.ScanLibrary))]
    [InlineData(nameof(ScannerService.ScanLibraries))]
    public void ScanJob_IsNotRetried(string methodName)
    {
        // Hangfire stores the runtime type, so the attributes on ScannerService are the ones that apply
        var method = typeof(ScannerService).GetMethod(methodName)!;
        var args = method.GetParameters().Select(p => p.ParameterType == typeof(int) ? (object) 1 : false).ToArray();
        var job = new Job(typeof(ScannerService), method, args);

        var retry = JobFilterProviders.Providers.GetFilters(job)
            .Select(f => f.Instance)
            .OfType<AutomaticRetryAttribute>()
            .Single();

        Assert.Equal(0, retry.Attempts);
    }
}
