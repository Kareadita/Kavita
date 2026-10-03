import {inject, Pipe, PipeTransform} from '@angular/core';
import {TranslocoService} from '@jsverse/transloco';

const PREFIX = 'upcoming-task-name-pipe';

/**
 * Names the recurring tasks GET server/activity returns as upcoming. Unknown ids show as sent
 */
@Pipe({
  name: 'upcomingTaskName',
  standalone: true
})
export class UpcomingTaskNamePipe implements PipeTransform {
  private readonly translocoService = inject(TranslocoService);

  transform(taskId: string): string {
    switch (taskId) {
      case 'scan-libraries': return this.translocoService.translate(`${PREFIX}.scan-libraries`);
      case 'cleanup': return this.translocoService.translate(`${PREFIX}.cleanup`);
      case 'backup': return this.translocoService.translate(`${PREFIX}.backup`);
      case 'sync-cbl': return this.translocoService.translate(`${PREFIX}.sync-cbl`);
      default: return taskId;
    }
  }

}
