import {Pipe, PipeTransform} from '@angular/core';

export const NULL_DATE = '0001-01-01T00:00:00';

/**
 * Responsible for taking 2 dates and transforming them into Mar. 1999 - Apr 2000.
 * Can optionally exclude the months.
 */
@Pipe({
  name: 'dateYearRange',
  standalone: true,
  pure: true,
})
export class DateYearRangePipe implements PipeTransform {

  transform(startDate: string | Date | null, endDate: string | Date | null = null, includeMonth = true): string {
    const locale = navigator.language;

    const isValid = (d: string | Date | null): boolean =>
      !!d && d !== NULL_DATE && !isNaN(new Date(d).getTime());

    const fmt = (d: string | Date): string => {
      const date = new Date(d);
      return includeMonth
        ? date.toLocaleDateString(locale, { month: 'short', year: 'numeric' })
        : date.toLocaleDateString(locale, { year: 'numeric' });
    };

    const hasStart = isValid(startDate);
    const hasEnd = isValid(endDate);

    if (hasStart && hasEnd) return `${fmt(startDate!)} – ${fmt(endDate!)}`;
    if (hasStart) return fmt(startDate!);
    if (hasEnd) return fmt(endDate!);

    return '';
  }

}
