import { Pipe, PipeTransform } from '@angular/core';
import { PublicationStatus } from '../_models/metadata/publication-status';

@Pipe({
  name: 'publicationStatus',
  standalone: true
})
export class PublicationStatusPipe implements PipeTransform {

  transform(value: PublicationStatus): string {
    switch (value) {
      case PublicationStatus.OnGoing: return 'Ongoing';
      case PublicationStatus.Hiatus: return 'Hiatus';
      case PublicationStatus.Completed: return 'Completed';
      case PublicationStatus.Cancelled: return 'Cancelled';
      case PublicationStatus.Ended: return 'Ended';

      default: return '';
    }
  }

}
