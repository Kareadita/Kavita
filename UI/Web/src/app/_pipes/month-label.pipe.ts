import { Pipe, PipeTransform } from '@angular/core';
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'monthLabel'
})
export class MonthLabelPipe implements PipeTransform {

  private readonly monthKeys = [
    'january', 'february', 'march', 'april', 'may', 'june',
    'july', 'august', 'september', 'october', 'november', 'december'
  ];

  transform(value: number, shortHand: boolean = true): string {
    if (value < 1 || value > 12) {
      console.error('Invalid month: ', value);
      throw new Error('Invalid month: ' + value);
    }

    return translate(this.getKey(this.monthKeys[value - 1], shortHand));
  }

  private getKey(key: string, shortHand: boolean): string {
    if (shortHand) {
      return 'month-label-pipe.' + key + '-short';
    }

    return 'month-label-pipe.' + key;
  }

}
