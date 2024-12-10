import { Pipe, PipeTransform } from '@angular/core';
import {TranslocoService} from "@jsverse/transloco";
import {HourEstimateRange} from "../_models/series-detail/hour-estimate-range";
import {DecimalPipe} from "@angular/common";

@Pipe({
  name: 'readTimeLeft',
  standalone: true
})
export class ReadTimeLeftPipe implements PipeTransform {

  constructor(private readonly translocoService: TranslocoService) {}

  transform(readingTimeLeft: HourEstimateRange): string {
    const hoursLabel = readingTimeLeft.avgHours > 1
      ? this.translocoService.translate('read-time-pipe.hours')
      : this.translocoService.translate('read-time-pipe.hour');

    const formattedHours = this.customRound(readingTimeLeft.avgHours);

    return `~${formattedHours} ${hoursLabel}`;
  }

  private customRound(value: number): string {
    const integerPart = Math.floor(value);
    const decimalPart = value - integerPart;

    if (decimalPart < 0.5) {
      // Round down to the nearest whole number
      return integerPart.toString();
    } else if (decimalPart >= 0.5 && decimalPart < 0.9) {
      // Return with 1 decimal place
      return value.toFixed(1);
    } else {
      // Round up to the nearest whole number
      return Math.ceil(value).toString();
    }
  }
}
