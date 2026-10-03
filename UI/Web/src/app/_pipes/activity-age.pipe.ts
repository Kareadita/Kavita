import {inject, Pipe, PipeTransform} from '@angular/core';
import {TranslocoService} from '@jsverse/transloco';

const PREFIX = 'activity-age-pipe';

/**
 * Short age for the activity time column: now, 8m, 2h. Rows are pruned after 24h, so there is no day form
 */
@Pipe({
  name: 'activityAge',
  standalone: true
})
export class ActivityAgePipe implements PipeTransform {
  private readonly translocoService = inject(TranslocoService);

  /**
   * @param now Pass a ticking value so the pipe re-runs
   */
  transform(utc: string, now: number): string {
    const minutes = Math.floor(Math.max(0, now - Date.parse(utc)) / 60_000);

    if (Number.isNaN(minutes) || minutes < 1) return this.translocoService.translate(`${PREFIX}.now`);
    if (minutes < 60) return this.translocoService.translate(`${PREFIX}.minutes`, {value: minutes});

    return this.translocoService.translate(`${PREFIX}.hours`, {value: Math.floor(minutes / 60)});
  }

}
