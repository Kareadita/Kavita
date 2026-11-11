using Kavita.Models.Entities.Enums;

namespace Kavita.Models.Images;

public sealed record ImagePolicy(EncodeFormat Format, CoverImageSize CoverSize);
