import {Pipe, PipeTransform} from '@angular/core';
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'cronFrequency',
  standalone: true,
  pure: true
})
export class CronFrequencyPipe implements PipeTransform {
  transform(value: string): string {
    return translate('cron-frequency-pipe.' + value);
  }
}
