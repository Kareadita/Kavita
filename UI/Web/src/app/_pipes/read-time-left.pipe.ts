import { Pipe, PipeTransform } from '@angular/core';
import {TranslocoService} from "@jsverse/transloco";
import {HourEstimateRange} from "../_models/series-detail/hour-estimate-range";

@Pipe({
  name: 'readTimeLeft',
  standalone: true
})
export class ReadTimeLeftPipe implements PipeTransform {

  constructor(private translocoService: TranslocoService) {}

  transform(readingTimeLeft: HourEstimateRange): string {
    return `~${readingTimeLeft.avgHours} ${readingTimeLeft.avgHours > 1 ? this.translocoService.translate('read-time-pipe.hours') : this.translocoService.translate('read-time-pipe.hour')}`;
  }
}
