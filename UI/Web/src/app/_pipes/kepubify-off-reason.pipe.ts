import {Pipe, PipeTransform} from '@angular/core';
import {translate} from '@jsverse/transloco';
import {KepubifyOffReason} from '../admin/_models/kepubify-off-reason';

@Pipe({
  name: 'kepubifyOffReason'
})
export class KepubifyOffReasonPipe implements PipeTransform {

  transform(value: KepubifyOffReason | null | undefined): string {
    switch (value) {
      case KepubifyOffReason.Disabled:
        return translate('kepubify-off-reason-pipe.disabled');
      case KepubifyOffReason.FileMissing:
        return translate('kepubify-off-reason-pipe.file-missing');
      case KepubifyOffReason.HashMismatch:
        return translate('kepubify-off-reason-pipe.hash-mismatch');
      case KepubifyOffReason.VersionCheckFailed:
        return translate('kepubify-off-reason-pipe.version-check-failed');
      default:
        return '';
    }
  }

}
