import {inject, Pipe, PipeTransform} from '@angular/core';
import {TranslocoService} from '@jsverse/transloco';
import {EventAction} from '../_models/events/event-action';

const PREFIX = 'event-action-pipe';

@Pipe({
  name: 'eventAction',
  standalone: true
})
export class EventActionPipe implements PipeTransform {
  private readonly translocoService = inject(TranslocoService);

  transform(value: EventAction): string {
    switch (value) {
      case EventAction.Reconnect: return this.translocoService.translate(`${PREFIX}.reconnect`);
      case EventAction.WhatsNew: return this.translocoService.translate(`${PREFIX}.whats-new`);
      case EventAction.RemindLater: return this.translocoService.translate(`${PREFIX}.remind-later`);
      case EventAction.OpenLibrary: return this.translocoService.translate(`${PREFIX}.open-library`);
      case EventAction.OpenSeries: return this.translocoService.translate(`${PREFIX}.open-series`);
      case EventAction.OpenMatching: return this.translocoService.translate(`${PREFIX}.open-matching`);
      case EventAction.Rescan: return this.translocoService.translate(`${PREFIX}.rescan`);
      case EventAction.ScanNow: return this.translocoService.translate(`${PREFIX}.scan-now`);
      case EventAction.Details: return this.translocoService.translate(`${PREFIX}.details`);
    }
  }

}
