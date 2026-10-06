namespace Kavita.Models.Parser;
#nullable enable

/// <summary>
/// What the scanner made of one file. A skipped file has neither an <see cref="Info"/> nor an <see cref="Issue"/>
/// </summary>
/// <param name="Info">Null when the file is not imported</param>
/// <param name="Issue">Set when something went wrong. With an <see cref="Info"/>, the file is still imported</param>
public sealed record ParseFileResult(ParserInfo? Info, ParseIssue? Issue = null)
{
    public bool IsFailed => Info == null && Issue != null;

    public static ParseFileResult Failed(ParseIssue issue) => new(null, issue);
}
