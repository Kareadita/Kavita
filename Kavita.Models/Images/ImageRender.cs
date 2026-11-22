using System.Collections.Generic;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.Images;

/// <param name="Quality">[0-100] and read by <see cref="EncodeFormat.JPEG"/> only. Null keeps the encoder's default</param>
public sealed record ImageRender(EncodeFormat Format, IReadOnlyList<IImageStep> Steps, int? Quality = null);
