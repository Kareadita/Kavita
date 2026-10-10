namespace Kavita.Services.Tests.Helpers;

/// <summary>
/// For tests that read jobs back from <see cref="Hangfire.JobStorage.Current"/>. Other classes swap it while they run
/// in parallel, so these run on their own
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class HangfireStorageCollection
{
    public const string Name = "Hangfire storage";
}
