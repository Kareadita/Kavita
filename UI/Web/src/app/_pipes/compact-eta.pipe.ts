import {Pipe, PipeTransform} from '@angular/core';

/**
 * Formats seconds remaining into a compact ETA string.
 * Examples: "3s", "2m 15s", "1h 5m", "2h 30m"
 */
@Pipe({
  name: 'compactEta',
  standalone: true
})
export class CompactEtaPipe implements PipeTransform {
  transform(seconds: number | null | undefined): string {
    if (seconds == null || seconds <= 0) return '';
    if (seconds < 60) return `${seconds}s`;

    const m = Math.floor(seconds / 60);
    const s = seconds % 60;

    if (m < 60) return s > 0 ? `${m}m ${s}s` : `${m}m`;

    const h = Math.floor(m / 60);
    const rm = m % 60;

    // TODO: Missing localization
    return rm > 0 ? `${h}h ${rm}m` : `${h}h`;
  }
}
