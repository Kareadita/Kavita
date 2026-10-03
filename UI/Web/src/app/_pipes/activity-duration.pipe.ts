import {inject, Pipe, PipeTransform} from '@angular/core';
import {TranslocoService} from '@jsverse/transloco';

const PREFIX = 'activity-duration-pipe';

@Pipe({
  name: 'activityDuration',
  standalone: true
})
export class ActivityDurationPipe implements PipeTransform {
  private readonly translocoService = inject(TranslocoService);

  transform(startUtc: string, endUtc: string): string {
    const total = Math.round(Math.max(0, Date.parse(endUtc) - Date.parse(startUtc)) / 1000);
    if (Number.isNaN(total)) return '';

    const hours = Math.floor(total / 3600);
    const minutes = Math.floor((total % 3600) / 60);
    const seconds = total % 60;

    if (hours > 0) return this.translocoService.translate(`${PREFIX}.hours`, {hours, minutes});
    if (minutes > 0) return this.translocoService.translate(`${PREFIX}.minutes`, {minutes, seconds});
    return this.translocoService.translate(`${PREFIX}.seconds`, {seconds});
  }

}
