import {Pipe, PipeTransform} from '@angular/core';
import {translate} from "@jsverse/transloco";
import {FolderWatchMode} from "../_models/library/folder-watch-mode";

@Pipe({
  name: 'folderWatchModeDescription'
})
export class FolderWatchModeDescriptionPipe implements PipeTransform {

  transform(value: FolderWatchMode | null | undefined): string {
    switch (value) {
      case FolderWatchMode.Off:
        return translate('folder-watch-mode-description-pipe.off');
      case FolderWatchMode.Automatic:
        return translate('folder-watch-mode-description-pipe.automatic');
      case FolderWatchMode.FileWatcher:
        return translate('folder-watch-mode-description-pipe.file-watcher');
      case FolderWatchMode.Polling:
        return translate('folder-watch-mode-description-pipe.polling');
      default:
        return '';
    }
  }

}
