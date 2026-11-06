import {Pipe, PipeTransform} from '@angular/core';
import {translate} from '@jsverse/transloco';
import {KepubifyCheckStatus} from '../admin/_models/kepubify-check-status';

@Pipe({
  name: 'kepubifyCheckStatus'
})
export class KepubifyCheckStatusPipe implements PipeTransform {

  transform(value: KepubifyCheckStatus | null | undefined): string {
    switch (value) {
      case KepubifyCheckStatus.Checking:
        return translate('kepubify-check-status-pipe.checking');
      case KepubifyCheckStatus.Supported:
        return translate('kepubify-check-status-pipe.supported');
      case KepubifyCheckStatus.NotSupported:
        return translate('kepubify-check-status-pipe.not-supported');
      default:
        return '';
    }
  }

}
