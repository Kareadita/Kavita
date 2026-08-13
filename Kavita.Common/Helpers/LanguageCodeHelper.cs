using System;
using System.Collections.Generic;
using System.Linq;

namespace Kavita.Common.Helpers;
#nullable enable

/// <summary>
/// Parsing and shape validation for semicolon separated lists of BCP-47 language codes.
/// </summary>
/// <remarks>
/// This deliberately validates <b>shape only</b> (RFC 5646 positional rules) and never membership in
/// <c>CultureInfo.GetCultures</c>. That list is the OS culture list on Windows but ICU's on Linux, and is
/// near-empty under InvariantGlobalization, so a code an admin validly entered on one host could vanish on
/// another. It also simply does not contain valid tags like <c>ja-Latn</c>, which is the whole point of the
/// romanization fallback.
/// </remarks>
public static class LanguageCodeHelper
{
    private const char Separator = ';';

    /// <summary>
    /// Reserved priority token that resolves to the provider's native (original-script) title rather than a
    /// BCP-47 language tag. See <see cref="IsReservedToken"/>.
    /// </summary>
    public const string NativeToken = "{Native}";

    /// <summary>
    /// Reserved priority token that resolves to the provider's romanized title. Unlike a fixed <c>ja-Latn</c>
    /// tag, it follows the series' real native language (a Korean work romanizes from <c>ko-Latn</c>).
    /// </summary>
    public const string RomajiToken = "{Romaji}";

    /// <summary>
    /// Splits a semicolon separated priority list into individual codes, highest priority first.
    /// </summary>
    public static IReadOnlyList<string> Split(string? codes) =>
        (codes ?? string.Empty).Split(Separator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Drops any code that is neither well-formed nor a reserved token, preserving order and re-joining with semicolons.
    /// </summary>
    public static string Sanitize(string? codes) =>
        string.Join(Separator, Split(codes).Where(c => IsWellFormed(c) || IsReservedToken(c)));

    /// <summary>
    /// Is the given code a reserved priority token (e.g. <c>{Native}</c>, <c>{Romaji}</c>) rather than a BCP-47
    /// language tag.
    /// </summary>
    /// <remarks>
    /// Reserved tokens resolve to a provider-supplied title slot that no language tag can name reliably: the
    /// native/romanized title's real language varies per series and per provider. Matched case-insensitively.
    /// </remarks>
    public static bool IsReservedToken(string? code) => IsNativeToken(code) || IsRomajiToken(code);

    /// <summary>Is the given code the <see cref="NativeToken"/>, matched case-insensitively.</summary>
    public static bool IsNativeToken(string? code) =>
        !string.IsNullOrWhiteSpace(code) && string.Equals(code, NativeToken, StringComparison.OrdinalIgnoreCase);

    /// <summary>Is the given code the <see cref="RomajiToken"/>, matched case-insensitively.</summary>
    public static bool IsRomajiToken(string? code) =>
        !string.IsNullOrWhiteSpace(code) && string.Equals(code, RomajiToken, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Is the given single code well-formed per the RFC 5646 subtag shape rules.
    /// </summary>
    /// <remarks>
    /// Positions are unambiguous by length: language is 2-3 alpha, an optional script is exactly 4 alpha,
    /// an optional region is 2 alpha or 3 digits. So <c>ja-Latn</c> passes while <c>ja-Ltn</c> (3 alpha,
    /// neither script nor region), <c>ja-Latin</c> (5 alpha) and <c>en_US</c> (underscore) do not.
    /// </remarks>
    public static bool IsWellFormed(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;

        var parts = code.Split('-');
        if (parts.Length is 0 or > 3) return false;

        // Language subtag
        if (!IsAlpha(parts[0]) || parts[0].Length is < 2 or > 3) return false;
        if (parts.Length == 1) return true;

        var next = 1;

        // Optional script subtag: exactly 4 alpha
        if (parts[next].Length == 4 && IsAlpha(parts[next]))
        {
            next++;
            if (parts.Length == next) return true;
        }

        // Optional region subtag: 2 alpha or 3 digits
        var region = parts[next];
        var isRegion = (region.Length == 2 && IsAlpha(region)) || (region.Length == 3 && IsDigits(region));

        return isRegion && parts.Length == next + 1;
    }

    private static bool IsAlpha(string value) => value.All(char.IsAsciiLetter);

    private static bool IsDigits(string value) => value.All(char.IsAsciiDigit);
}
