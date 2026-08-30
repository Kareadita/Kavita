/** Generates a unique string for label/input id wiring **/
export function generateUniqueId(): string {
  try {
    if (crypto && crypto.randomUUID) {
      return 'id-' + crypto.randomUUID();
    }
  } catch(ex) { /* Swallow */ }

  // Fallback for browsers without crypto.randomUUID (which has happened multiple times in my user base)
  return 'id-' + Math.random().toString(36).slice(2, 11) + '-' + Date.now().toString(36);
}
