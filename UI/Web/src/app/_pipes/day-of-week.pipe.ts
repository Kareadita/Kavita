import {Pipe, PipeTransform} from '@angular/core';
import { DayOfWeek } from 'src/app/_services/statistics.service';
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'dayOfWeek',
  standalone: true
})
export class DayOfWeekPipe implements PipeTransform {

  transform(value: DayOfWeek): string {
    switch(value) {
      case DayOfWeek.Monday:
        return translate('day-of-week-pipe.monday');
      case DayOfWeek.Tuesday:
        return translate('day-of-week-pipe.tuesday');
      case DayOfWeek.Wednesday:
        return translate('day-of-week-pipe.wednesday');
      case DayOfWeek.Thursday:
        return translate('day-of-week-pipe.thursday');
      case DayOfWeek.Friday:
        return translate('day-of-week-pipe.friday');
      case DayOfWeek.Saturday:
        return translate('day-of-week-pipe.saturday');
      case DayOfWeek.Sunday:
        return translate('day-of-week-pipe.sunday');

    }
  }

}
