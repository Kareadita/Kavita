using Kavita.Models.Entities.Enums;

namespace Kavita.Models.Images;

public sealed record CoverWrite(
    ImageOrigin Origin,
    bool Lock,
    bool ChooseBetter = false,
    bool RejectPlaceholder = false);
