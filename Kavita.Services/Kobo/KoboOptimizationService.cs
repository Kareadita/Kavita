using Kavita.API.Services.Kobo;

namespace Kavita.Services.Kobo;

/// <summary>
/// Handles all things Kobo Optimization (Archive -> Epub Optimization)
/// </summary>
public class KoboOptimizationService : IKoboOptimizationService
{
    // Device Profile, Direction, Steps, Kobo Cover and Cached kepub file all belong here
    // EpubBuilder responsible for turning into an Epub (agnostic to Kobo)
}
