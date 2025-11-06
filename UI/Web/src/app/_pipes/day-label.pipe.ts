import { Pipe, PipeTransform } from '@angular/core';
import {translate} from "@jsverse/transloco";

/**
 * Starts with Sunday
 */
@Pipe({
  name: 'dayLabel'
})
export class DayLabelPipe implements PipeTransform {

  protected readonly dayLabels = ['sunday', 'monday', 'tuesday',
    'wednesday', 'thursday', 'friday', 'saturday'];

  transform(value: number, shortHand: boolean = true): string {
    if (value < 0 || value > 6) {
      console.error('Invalid day: ', value);
      throw new Error('Invalid day: ' + value);
    }

    return translate(this.getKey(this.dayLabels[value], shortHand));
  }

  private getKey(key: string, shortHand: boolean): string {
    if (shortHand) {
      return 'day-label-pipe.' + key + '-short';
    }

    return 'day-label-pipe.' + key;
  }

}
