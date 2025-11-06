import {inject, Pipe, PipeTransform} from '@angular/core';
import {TranslocoService} from "@jsverse/transloco";

@Pipe({
  name: 'ordinalDate'
})
export class OrdinalDatePipe implements PipeTransform {

  private readonly transloco = inject(TranslocoService);

  transform(value: Date | string | number | null): string | null {
    if (value === null) return null;

    const date = new Date(value);
    const day = date.getDate();
    const lang = this.transloco.getActiveLang();

    const formatter = new Intl.DateTimeFormat(lang, { month: 'short' });
    const month = formatter.format(date);

    const ordinalDay = this.getOrdinalDay(day, lang);

    // Use transloco for the format pattern
    return this.transloco.translate('ordinal-date-pipe.ordinalFormat', {
      month,
      day: ordinalDay
    });
  }

  private getOrdinalDay(n: number, lang: string): string {
    // Get the ordinal rule key
    const v = n % 100;
    let key: string;

    if (v >= 11 && v <= 13) {
      key = 'th';
    } else {
      const lastDigit = n % 10;
      switch (lastDigit) {
        case 1: key = 'st'; break;
        case 2: key = 'nd'; break;
        case 3: key = 'rd'; break;
        default: key = 'th';
      }
    }

    const formattedNumber = this.formatDayNumber(n, lang);
    const suffix = this.transloco.translate(`ordinal-date-pipe.ordinal.${key}`);

    return `${formattedNumber}${suffix}`;
  }

  private formatDayNumber(n: number, lang: string): string {
    return new Intl.NumberFormat(lang).format(n);
  }

}
