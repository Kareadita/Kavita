/**
 * Shared pre-login locale logic (pure functions, easy to unit test).
 * Priority: explicit user choice > browser auto-match > default English.
 * Auto-match and explicit choice only touch localStorage; whether the
 * choice is persisted to the database is decided by the caller.
 */

// localStorage keys (kavita-locale keeps the legacy key so existing users migrate seamlessly)
export const KavitaLocaleKey = 'kavita-locale';
// Where the stored locale came from: explicit (user picked it) or auto (matched at startup)
export const KavitaLocaleSourceKey = 'kavita-locale-source';
export const LocaleSourceExplicit = 'explicit';
export const LocaleSourceAuto = 'auto';
// Set after the first server-side promotion so we never overwrite the account locale again
export const KavitaLocalePromotedKey = 'kavita-locale-promoted';

/**
 * Translation files actually shipped (assets/langs/*.json). Auto-match only
 * lands on these, otherwise HttpLoader 404s and pops an error toast.
 * Manual dropdown lists server locales and is not limited by this.
 * Append here when a new language file ships.
 */
const ShippedLangs = new Set([
  'ar', 'ca', 'cs', 'da', 'de', 'el', 'en', 'es', 'et', 'fi', 'fr', 'ga',
  'hi', 'hr', 'hu', 'id', 'it', 'ja', 'ko', 'lt', 'lv', 'ms', 'nb_NO',
  'nl', 'pl', 'pt', 'pt_BR', 'pt_PT', 'ru', 'sk', 'sl', 'sv', 'ta', 'th',
  'tr', 'uk', 'vi', 'zh_Hans', 'zh_Hant'
]);

/** Storage access that never throws (private mode can raise SecurityError synchronously) */
export function safeGet(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

/** Storage write that never throws */
export function safeSet(key: string, value: string): void {
  try {
    localStorage.setItem(key, value);
  } catch {
    /* storage unavailable: current page still switches, choice just won't persist */
  }
}

/**
 * Browser tag (zh-CN) to Kavita file style (zh_CN)
 */
export function normalizeLocaleTag(tag: string): string {
  return (tag || '').trim().replace(/-/g, '_');
}

/**
 * Chinese special mapping: Simplified by default, Traditional for TW/HK/MO
 */
function mapChinese(base: string, region: string): string | null {
  if (base !== 'zh') return null;
  if (['TW', 'HK', 'MO'].includes(region.toUpperCase())) return 'zh_Hant';
  return 'zh_Hans';
}

/**
 * Match one browser tag against available languages. The hit must be a
 * shipped translation file. Candidate order: exact (hyphen/underscore
 * insensitive) -> Chinese special mapping -> same-language prefix fallback.
 * Pass Transloco's availableLangs as `available`; it only gates supported families.
 */
export function matchSingleLocale(tag: string, available: Array<string>): string | null {
  const normalized = normalizeLocaleTag(tag);
  if (!normalized) return null;

  const availableSet = new Set(available.map(a => normalizeLocaleTag(a)));
  const parts = normalized.split('_');
  const base = parts[0];
  const region = parts[parts.length - 1];

  const candidates: Array<string> = [normalized];
  const chinese = mapChinese(base, region || '');
  if (chinese) candidates.push(chinese);
  if (base && base !== normalized) candidates.push(base);

  for (const candidate of candidates) {
    if (!availableSet.has(candidate)) continue;
    // Underscore spelling first (backend FileName uses underscores), and it must be shipped
    const underscore = candidate.replace(/-/g, '_');
    if (ShippedLangs.has(underscore)) {
      return available.find(a => normalizeLocaleTag(a) === underscore) || underscore;
    }
    if (ShippedLangs.has(candidate)) {
      return available.find(a => normalizeLocaleTag(a) === candidate) || candidate;
    }
  }

  return null;
}

/**
 * Walk browser language preferences in order, return the first Kavita match
 */
export function matchBrowserLocale(browserLangs: Array<string>, available: Array<string>): string | null {
  for (const lang of browserLangs || []) {
    const hit = matchSingleLocale(lang, available);
    if (hit) return hit;
  }
  return null;
}

/**
 * Dropdown display name. Backend RenderName is CultureInfo.EnglishName
 * (always English); override the two Chinese entries with native names,
 * leave everything else untouched to keep the change surgical.
 */
export function displayLocaleName(fileName: string, renderName: string): string {
  if (fileName === 'zh_Hans') return '简体中文';
  if (fileName === 'zh_Hant') return '繁體中文';
  return renderName;
}
