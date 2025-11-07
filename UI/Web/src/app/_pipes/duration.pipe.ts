import { Pipe, PipeTransform } from '@angular/core';
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'duration'
})
export class DurationPipe implements PipeTransform {

  transform(seconds: number): string {
    if (seconds < 60) return translate('duration-pipe.seconds', {num: seconds});
    if (seconds < 3600) return translate('duration-pipe.minutes', {num: Math.floor(seconds / 60)});

    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);

    return minutes > 0 ? translate('duration-pipe.combo', {hour: hours, min: minutes}) : translate('duration-pipe.hours', {num: hours});
  }

}
