import { Pipe, PipeTransform } from '@angular/core';
import {IHasReadingTime} from "../_models/common/i-has-reading-time";
import {TranslocoService} from "@jsverse/transloco";

@Pipe({
  name: 'readTime',
  standalone: true
})
export class ReadTimePipe implements PipeTransform {
  constructor(private translocoService: TranslocoService) {}

  transform(readingTime: IHasReadingTime): string {
    if (readingTime.maxHoursToRead === 0 || readingTime.minHoursToRead === 0) {
        return this.translocoService.translate('read-time-pipe.less-than-hour');
    } else {
      return `${readingTime.minHoursToRead}${readingTime.maxHoursToRead !== readingTime.minHoursToRead ? ('-' + readingTime.maxHoursToRead) : ''}` +
        ` ${readingTime.minHoursToRead > 1 ? this.translocoService.translate('read-time-pipe.hours') : this.translocoService.translate('read-time-pipe.hour')}`;
    }
  }

}
