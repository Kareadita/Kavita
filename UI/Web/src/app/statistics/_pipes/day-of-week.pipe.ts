import {inject, Pipe, PipeTransform} from '@angular/core';
import { DayOfWeek } from 'src/app/_services/statistics.service';
import {TranslocoService} from "@ngneat/transloco";

@Pipe({
    name: 'dayOfWeek',
    standalone: true
})
export class DayOfWeekPipe implements PipeTransform {

  translocoService = inject(TranslocoService);

  transform(value: DayOfWeek): string {
    switch(value) {
      case DayOfWeek.Monday:
        return this.translocoService.translate('day-of-week-pipe.monday');
      case DayOfWeek.Tuesday:
        return this.translocoService.translate('day-of-week-pipe.tuesday');
      case DayOfWeek.Wednesday:
        return this.translocoService.translate('day-of-week-pipe.wednesday');
      case DayOfWeek.Thursday:
        return this.translocoService.translate('day-of-week-pipe.thursday');
      case DayOfWeek.Friday:
        return this.translocoService.translate('day-of-week-pipe.friday');
      case DayOfWeek.Saturday:
        return this.translocoService.translate('day-of-week-pipe.saturday');
      case DayOfWeek.Sunday:
        return this.translocoService.translate('day-of-week-pipe.sunday');

    }
  }

}
