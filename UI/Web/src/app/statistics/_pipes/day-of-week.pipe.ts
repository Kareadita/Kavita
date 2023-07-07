import { Pipe, PipeTransform } from '@angular/core';
import { DayOfWeek } from 'src/app/_services/statistics.service';

@Pipe({
    name: 'dayOfWeek',
    standalone: true
})
export class DayOfWeekPipe implements PipeTransform {

  transform(value: DayOfWeek): string {
    switch(value) {
      case DayOfWeek.Monday: return 'Monday';
      case DayOfWeek.Tuesday: return 'Tuesday';
      case DayOfWeek.Wednesday: return 'Wednesday';
      case DayOfWeek.Thursday: return 'Thursday';
      case DayOfWeek.Friday: return 'Friday';
      case DayOfWeek.Saturday: return 'Saturday';
      case DayOfWeek.Sunday: return 'Sunday';

    }
  }

}
