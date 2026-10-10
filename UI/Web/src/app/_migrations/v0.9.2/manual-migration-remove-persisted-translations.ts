const persistedTranslationKeys = [
  'transloco--kavita',
  'transloco--kavita/timestamp',
  '@transloco/translations',
  '@transloco/translations/timestamp',
  'translocoLang',
];

/**
 * Added in v0.9.2. Before this version, @jsverse/transloco-persist-translations cached every loaded language
 * in localStorage (about 200 KB each).
 *
 * Safe to remove
 */
export function manualMigrationRemovePersistedTranslations() {
  persistedTranslationKeys.forEach(key => localStorage.removeItem(key));
}
