import {inject, Pipe, PipeTransform} from '@angular/core';
import {TranslocoService} from "@jsverse/transloco";

/**
 * Converts hours -> days, months, years, etc
 */
@Pipe({
  name: 'timeDuration',
  standalone: true
})
export class TimeDurationPipe implements PipeTransform {

  translocoService = inject(TranslocoService);

  transform(hours: number): string {
    if (hours === 0)
      return this.translocoService.translate('time-duration-pipe.hours', {value: hours});
    if (hours < 1) {
      return this.translocoService.translate('time-duration-pipe.minutes', {value: (hours * 60).toFixed(1)});
    } else if (hours < 24) {
      return this.translocoService.translate('time-duration-pipe.hours', {value: hours.toFixed(1)});
    } else if (hours < 720) {
      return this.translocoService.translate('time-duration-pipe.days', {value: (hours / 24).toFixed(1)});
    } else if (hours < 8760) {
      return this.translocoService.translate('time-duration-pipe.months', {value: (hours / 720).toFixed(1)});
    } else {
      return this.translocoService.translate('time-duration-pipe.years', {value: (hours / 8760).toFixed(1)});
    }
  }

}
