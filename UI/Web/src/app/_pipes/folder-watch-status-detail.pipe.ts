import {Pipe, PipeTransform} from '@angular/core';
import {translate} from '@jsverse/transloco';
import {FolderWatchStatus} from '../_models/library/folder-watch-status';
import {FolderWatchMethod} from '../_models/library/folder-watch-method';
import {FolderWatchMode} from '../_models/library/folder-watch-mode';
import {FolderWatchFallbackReason} from '../_models/events/core/folder-watch-fallback-reason';

const PREFIX = 'folder-watch-status-detail-pipe';

/**
 * One line under the status badge: why the library is in this state and what moves it on. Empty when the badge says it all
 */
@Pipe({
  name: 'folderWatchStatusDetail'
})
export class FolderWatchStatusDetailPipe implements PipeTransform {

  transform(status: FolderWatchStatus | null | undefined): string {
    if (!status) return '';
    if (!status.enableFolderWatching) return translate(`${PREFIX}.server-off`);

    const folder = status.folder ?? '';
    switch (status.method) {
      case FolderWatchMethod.Trial:
        return status.successes >= status.successesNeeded && status.needsLibraryScan
          ? translate(`${PREFIX}.trial-waiting-scan`, {successes: status.successes, needed: status.successesNeeded})
          : translate(`${PREFIX}.trial`, {successes: status.successes, needed: status.successesNeeded});
      case FolderWatchMethod.FileWatcher:
        return status.reason === FolderWatchFallbackReason.MissedChange
          ? translate(`${PREFIX}.missed-change`, {folder})
          : '';
      case FolderWatchMethod.Polling:
        return status.mode === FolderWatchMode.Polling
          ? translate(`${PREFIX}.polling`)
          : translate(`${PREFIX}.polling-network`);
      case FolderWatchMethod.Failed:
        return status.reason === FolderWatchFallbackReason.MissedChange
          ? translate(`${PREFIX}.missed-change`, {folder})
          : translate(`${PREFIX}.errors`, {folder});
      default:
        return '';
    }
  }

}
