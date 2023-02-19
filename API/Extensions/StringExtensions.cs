using System.Text.RegularExpressions;

namespace API.Extensions;

public static class StringExtensions
{
    private static readonly Regex SentenceCaseRegex = new Regex(@"(^[a-z])|\.\s+(.)",
        RegexOptions.ExplicitCapture | RegexOptions.Compiled, Services.Tasks.Scanner.Parser.Parser.RegexTimeout);

    public static string SentenceCase(this string value)
    {
        return SentenceCaseRegex.Replace(value.ToLower(), s => s.Value.ToUpper());
    }
}
