using Kavita.Models.Entities.Enums;

namespace Kavita.Models.Parser;

/// <summary>
/// What went wrong with one file while the scanner read it
/// </summary>
public sealed record ParseIssue(MediaErrorReason Reason, string Details);
