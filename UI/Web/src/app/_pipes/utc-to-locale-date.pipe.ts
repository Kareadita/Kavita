import { Pipe, PipeTransform } from '@angular/core';
import {DateTime} from "luxon";

@Pipe({
  name: 'utcToLocaleDate',
  standalone: true
})
/**
 * This is the same as the UtcToLocalTimePipe but returning a timezone aware DateTime object rather than a string.
 * Use this when the next operation needs a Date object (like the TimeAgoPipe)
 */
export class UtcToLocaleDatePipe implements PipeTransform {

  transform(utcDate: string | undefined | null): Date {
    if (utcDate === '' || utcDate === null || utcDate === undefined || utcDate.split('T')[0] === '0001-01-01')  {
      // Not sure what I should return here? Unix 0?
      // TODO: On PR review
      return null!;
    }

    const browserLanguage = navigator.language;
    const dateTime = DateTime.fromISO(utcDate, { zone: 'utc' }).toLocal().setLocale(browserLanguage);
    return dateTime.toJSDate()
  }

}
