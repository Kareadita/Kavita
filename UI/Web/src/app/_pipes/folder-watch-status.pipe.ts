import {Pipe, PipeTransform} from '@angular/core';
import {translate} from '@jsverse/transloco';
import {FolderWatchStatus} from '../_models/library/folder-watch-status';
import {FolderWatchMethod} from '../_models/library/folder-watch-method';

const PREFIX = 'folder-watch-status-pipe';

/**
 * Badge label for how a library notices changes now. {@link FolderWatchStatusDetailPipe} gives the why
 */
@Pipe({
  name: 'folderWatchStatus'
})
export class FolderWatchStatusPipe implements PipeTransform {

  transform(status: FolderWatchStatus | null | undefined): string {
    if (!status) return '';
    if (!status.enableFolderWatching) return translate(`${PREFIX}.server-off`);

    switch (status.method) {
      case FolderWatchMethod.Off:
        return translate(`${PREFIX}.off`);
      case FolderWatchMethod.Trial:
        return translate(`${PREFIX}.trial`);
      case FolderWatchMethod.FileWatcher:
        return translate(`${PREFIX}.watching`);
      case FolderWatchMethod.Polling:
      case FolderWatchMethod.Failed:
        return translate(`${PREFIX}.polling`);
      default:
        return '';
    }
  }

}
