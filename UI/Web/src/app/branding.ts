// Iterverse-specific rebrand of this Kavita fork. Kept as a single named
// constant, referenced everywhere the product name appears in code,
// rather than scattered literal strings - the goal is that a future
// name change (or a future upstream merge) only has to touch this file,
// not hunt through every component for the word "Kavita".
//
// Deliberately does NOT touch anything under the "Kavita+" naming
// (KavitaPlus, kavita-plus-*) - that's Kavita's own real, separate paid
// service this app can still talk to, not this app's own display
// branding. Renaming those would break a real integration for no reason.
export const APP_NAME = 'Iterverse Library';
