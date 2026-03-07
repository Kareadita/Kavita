import {DateTime} from "luxon";

/**
 * Normalizes a timestamp value (either legacy numeric ms or ISO string) to a UTC ISO string.
 * Returns empty string for falsy values.
 */
export function normalizeTimestamp(val: string | number | undefined | null): string {
  if (!val) return '';
  if (typeof val === 'number') {
    return DateTime.fromMillis(val, {zone: 'utc'}).toISO()!;
  }
  return val;
}
