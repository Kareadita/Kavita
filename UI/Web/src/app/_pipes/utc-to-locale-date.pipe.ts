import { Pipe, PipeTransform } from '@angular/core';
import {DateTime} from "luxon";

@Pipe({
  name: 'utcToLocalDate',
  standalone: true
})
/**
 * This is the same as the UtcToLocalTimePipe but returning a timezone aware DateTime object rather than a string.
 * Use this when the next operation needs a Date object (like the TimeAgoPipe)
 */
export class UtcToLocalDatePipe implements PipeTransform {

  transform(utcDate: string | undefined | null): Date | null {
    if (utcDate === '' || utcDate === null || utcDate === undefined || utcDate.split('T')[0] === '0001-01-01')  {
      return null;
    }

    const browserLanguage = navigator.language;
    const dateTime = DateTime.fromISO(utcDate, { zone: 'utc' }).toLocal().setLocale(browserLanguage);
    return dateTime.toJSDate()
  }

}
