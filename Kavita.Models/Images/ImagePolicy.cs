using Kavita.Models.Entities.Enums;

namespace Kavita.Models.Images;

/// <summary>
/// The server settings an image write follows, read from the settings at the time of the write
/// </summary>
/// <param name="Format">Encoding the file is saved in</param>
/// <param name="CoverSize">Dimensions used by kinds sized with <see cref="ImageSizeRule.CoverSize"/></param>
public sealed record ImagePolicy(EncodeFormat Format, CoverImageSize CoverSize);
