using NetVips;

namespace Kavita.Models.Images;

public readonly record struct ThumbnailTarget(int Width, int Height, Enums.Size Size, Enums.Interesting? Crop);
