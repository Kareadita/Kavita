import {Pipe, PipeTransform} from '@angular/core';
import {translate} from '@jsverse/transloco';
import {FolderWatchFallbackReason} from '../_models/events/core/folder-watch-fallback-reason';

@Pipe({
  name: 'folderWatchFallbackReason'
})
export class FolderWatchFallbackReasonPipe implements PipeTransform {

  transform(value: FolderWatchFallbackReason | null | undefined): string {
    switch (value) {
      case FolderWatchFallbackReason.Errors:
        return translate('folder-watch-fallback-reason-pipe.errors');
      case FolderWatchFallbackReason.MissedChange:
        return translate('folder-watch-fallback-reason-pipe.missed-change');
      default:
        return '';
    }
  }

}
