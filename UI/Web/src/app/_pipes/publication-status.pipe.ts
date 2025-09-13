import {Pipe, PipeTransform} from '@angular/core';
import { PublicationStatus } from '../_models/metadata/publication-status';
import {TranslocoService} from "@jsverse/transloco";

@Pipe({
  name: 'publicationStatus',
  standalone: true
})
export class PublicationStatusPipe implements PipeTransform {
  constructor(private translocoService: TranslocoService) {}

  transform(value: PublicationStatus): string {
    switch (value) {
      case PublicationStatus.OnGoing:
        return this.translocoService.translate('publication-status-pipe.ongoing');
      case PublicationStatus.Hiatus:
        return this.translocoService.translate('publication-status-pipe.hiatus');
      case PublicationStatus.Completed:
        return this.translocoService.translate('publication-status-pipe.completed');
      case PublicationStatus.Cancelled:
        return this.translocoService.translate('publication-status-pipe.cancelled');
      case PublicationStatus.Ended:
        return this.translocoService.translate('publication-status-pipe.ended');
      default:
        return '';
    }
  }

}
