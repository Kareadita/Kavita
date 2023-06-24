import { Pipe, PipeTransform } from '@angular/core';

/**
 * Converts hours -> days, months, years, etc
 */
@Pipe({
  name: 'timeDuration',
  standalone: true
})
export class TimeDurationPipe implements PipeTransform {

  transform(hours: number): string {
    if (hours === 0) return `${hours} hours`;
    if (hours < 1) {
      return `${(hours * 60).toFixed(1)} minutes`;
    } else if (hours < 24) {
      return `${hours.toFixed(1)} hours`;
    } else if (hours < 720) {
      return `${(hours / 24).toFixed(1)} days`;
    } else if (hours < 8760) {
      return `${(hours / 720).toFixed(1)} months`;
    } else {
      return `${(hours / 8760).toFixed(1)} years`;
    }
  }

}
