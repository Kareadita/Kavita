import { Pipe, PipeTransform } from '@angular/core';
import { DateTime } from 'luxon';

type UtcToLocalTimeFormat = 'full' | 'short' | 'shortDate' | 'shortTime';

  // FULL = 'full', // 'EEE, MMMM d, y, h:mm:ss a zzzz' - Monday, June 15, 2015 at 9:03:01 AM GMT+01:00
  // SHORT = 'short', // 'd/M/yy, h:mm - 15/6/15, 9:03
  // SHORT_DATE = 'shortDate', // 'd/M/yy' - 15/6/15
  // SHORT_TIME = 'shortTime',  // 'h:mm' - 9:03


@Pipe({
  name: 'utcToLocalTime',
  standalone: true
})
export class UtcToLocalTimePipe implements PipeTransform {

  transform(utcDate: string | undefined | null, format: UtcToLocalTimeFormat = 'short'): string {
    if (utcDate === '' || utcDate === null || utcDate === undefined || utcDate.split('T')[0] === '0001-01-01')  {
      return '';
    }

    const browserLanguage = navigator.language;
    const dateTime = DateTime.fromISO(utcDate, { zone: 'utc' }).toLocal().setLocale(browserLanguage);

    switch (format) {
      case 'short':
        return dateTime.toLocaleString(DateTime.DATETIME_SHORT);
      case 'shortDate':
        return dateTime.toLocaleString(DateTime.DATE_SHORT);
      case 'shortTime':
        return dateTime.toLocaleString(DateTime.TIME_SIMPLE);
      case 'full':
        return dateTime.toString();
      default:
        console.error('No logic in place for utc date format, format: ', format);
        return utcDate;
    }
  }

}
