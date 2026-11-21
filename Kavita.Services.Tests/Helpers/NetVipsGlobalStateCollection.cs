namespace Kavita.Services.Tests.Helpers;

/// <summary>
/// For tests that change process-wide libvips settings such as <c>NetVips.BlockUntrusted</c>. Every other image test
/// would run under the changed setting, so these run on their own
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class NetVipsGlobalStateCollection
{
    public const string Name = "NetVips global state";
}
