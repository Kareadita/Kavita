import {AbstractControl, ValidationErrors, ValidatorFn} from "@angular/forms";

/**
 * Utilities for semicolon separated lists of BCP-47 language codes, used by the Kavita+
 * Series Name/LocalizedName language priority settings.
 *
 * Validation is split into two deliberate tiers:
 *  - Shape (error): a token that cannot be a valid tag on any host. Blocks saving.
 *  - Membership (warning): a well-formed token whose language or script we don't recognise. Never blocks.
 *
 * Membership can only ever warn because the code list comes from `CultureInfo.GetCultures`, which is the OS
 * culture list on Windows but ICU's on Linux and near-empty under InvariantGlobalization. It also simply does
 * not contain valid tags like `ja-Latn` (romanized Japanese is not a locale anyone ships), which is precisely
 * the fallback this feature exists to support.
 */

export const LANGUAGE_CODE_SEPARATOR = ';';

/**
 * Reserved priority token that resolves to the provider's native (original-script) title rather than a BCP-47
 * language tag. The native title's real language varies per series and per provider, so no tag names it reliably.
 */
export const NATIVE_TOKEN = '{Native}';

/**
 * Reserved priority token that resolves to the provider's romanized title. Unlike a fixed `ja-Latn` tag, it
 * follows the series' real native language (a Korean work romanizes from `ko-Latn`).
 */
export const ROMAJI_TOKEN = '{Romaji}';

const RESERVED_TOKENS: ReadonlyArray<string> = [NATIVE_TOKEN, ROMAJI_TOKEN];

/**
 * Is the given code a reserved priority token (e.g. `{Native}`, `{Romaji}`) rather than a BCP-47 language tag.
 * Case-insensitive.
 */
export function isReservedToken(code: string): boolean {
  return !!code && RESERVED_TOKENS.some(t => t.toLowerCase() === code.toLowerCase());
}

/**
 * Is the given code the `{Native}` token, matched case-insensitively.
 */
export function isNativeToken(code: string): boolean {
  return !!code && code.toLowerCase() === NATIVE_TOKEN.toLowerCase();
}

/**
 * Is the given code the `{Romaji}` token, matched case-insensitively.
 */
export function isRomajiToken(code: string): boolean {
  return !!code && code.toLowerCase() === ROMAJI_TOKEN.toLowerCase();
}

/**
 * Well-known ISO 15924 script subtags.
 *
 * Intentionally not exhaustive (ISO 15924 defines ~200) and intentionally only used to warn. Note that
 * Grek/Hebr/Jpan/Kore never appear in .NET's culture list, because they are the default script for their
 * language and so never need disambiguating into a locale name.
 *
 * https://appmakers.substack.com/p/bcp-47-language-codes-list?utm_campaign=post&utm_medium=web
 */
export const KNOWN_SCRIPT_SUBTAGS: ReadonlySet<string> = new Set([
  // Commonly referenced
  'latn', 'cyrl', 'arab', 'hans', 'hant', 'deva', 'grek', 'hebr', 'jpan', 'kore',
  // Present in .NET's culture list
  'adlm', 'beng', 'cakm', 'cans', 'cher', 'guru', 'java', 'mong', 'olck', 'tfng', 'vaii',
  // Common scripts absent from both of the above
  'thai', 'taml', 'kana', 'hira', 'hang', 'hani', 'armn', 'geor', 'ethi', 'khmr',
  'sinh', 'mlym', 'telu', 'knda', 'gujr', 'orya', 'tibt', 'mymr', 'laoo', 'syrc',
]);

const ALPHA = /^[a-zA-Z]+$/;
const DIGITS = /^[0-9]+$/;

/**
 * Splits a semicolon separated priority list into individual codes, highest priority first.
 */
export function splitLanguageCodes(codes: string | null | undefined): Array<string> {
  return (codes || '')
    .split(LANGUAGE_CODE_SEPARATOR)
    .map(c => c.trim())
    .filter(c => c.length > 0);
}

/**
 * Is the given single code well-formed per the RFC 5646 subtag shape rules.
 *
 * Positions are unambiguous by length: language is 2-3 alpha, an optional script is exactly 4 alpha, an
 * optional region is 2 alpha or 3 digits. So `ja-Latn` passes while `ja-Ltn` (3 alpha, neither script nor
 * region), `ja-Latin` (5 alpha) and `en_US` (underscore) do not.
 */
export function isWellFormedLanguageCode(code: string): boolean {
  if (!code) return false;

  const parts = code.split('-');
  if (parts.length === 0 || parts.length > 3) return false;

  if (!ALPHA.test(parts[0]) || parts[0].length < 2 || parts[0].length > 3) return false;
  if (parts.length === 1) return true;

  let next = 1;

  // Optional script subtag: exactly 4 alpha
  if (parts[next].length === 4 && ALPHA.test(parts[next])) {
    next++;
    if (parts.length === next) return true;
  }

  // Optional region subtag: 2 alpha or 3 digits
  const region = parts[next];
  const isRegion = (region.length === 2 && ALPHA.test(region)) || (region.length === 3 && DIGITS.test(region));

  return isRegion && parts.length === next + 1;
}

/**
 * The language subtag of a code, lowercased. `ja-Latn` returns `ja`.
 */
export function primarySubtag(code: string): string {
  return code.split('-')[0].toLowerCase();
}

/**
 * The script subtag of a well-formed code, lowercased, or null when it has none.
 */
export function scriptSubtag(code: string): string | null {
  const parts = code.split('-');
  if (parts.length < 2) return null;

  const candidate = parts[1];
  return candidate.length === 4 && ALPHA.test(candidate) ? candidate.toLowerCase() : null;
}

/**
 * Error-tier validator. Flags only codes that are malformed, which blocks saving.
 *
 * Deliberately does NOT flag unrecognized languages or scripts - those are surfaced separately as warnings so
 * that `form.valid` continues to mean "safe to save".
 */
export function languageCodeListValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const malformed = splitLanguageCodes(control.value)
      .filter(c => !isWellFormedLanguageCode(c) && !isReservedToken(c));

    return malformed.length > 0 ? {malformedLanguageCodes: malformed} : null;
  };
}

/**
 * Warning-tier check. Returns well-formed codes whose language subtag is not in the supplied set.
 *
 * Returns nothing when the set is empty or implausibly small, which is what an InvariantGlobalization server
 * looks like - flagging every code there would be noise, not signal.
 */
export function unknownLanguageSubtags(codes: string | null | undefined, knownPrimarySubtags: ReadonlySet<string>): Array<string> {
  if (knownPrimarySubtags.size < 10) return [];

  return splitLanguageCodes(codes)
    .filter(isWellFormedLanguageCode)
    .filter(c => !knownPrimarySubtags.has(primarySubtag(c)));
}

/**
 * Warning-tier check. Returns well-formed codes carrying a script subtag we don't recognise.
 */
export function unknownScriptSubtags(codes: string | null | undefined): Array<string> {
  return splitLanguageCodes(codes)
    .filter(isWellFormedLanguageCode)
    .filter(c => {
      const script = scriptSubtag(c);
      return script !== null && !KNOWN_SCRIPT_SUBTAGS.has(script);
    });
}
