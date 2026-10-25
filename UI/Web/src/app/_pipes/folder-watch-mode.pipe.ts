import {Pipe, PipeTransform} from '@angular/core';
import {translate} from "@jsverse/transloco";
import {FolderWatchMode} from "../_models/library/folder-watch-mode";

@Pipe({
  name: 'folderWatchMode'
})
export class FolderWatchModePipe implements PipeTransform {

  transform(value: FolderWatchMode | null | undefined, markRecommended = false): string {
    switch (value) {
      case FolderWatchMode.Off:
        return translate('folder-watch-mode-pipe.off');
      case FolderWatchMode.Automatic:
        return translate(markRecommended ? 'folder-watch-mode-pipe.automatic-recommended' : 'folder-watch-mode-pipe.automatic');
      case FolderWatchMode.FileWatcher:
        return translate('folder-watch-mode-pipe.file-watcher');
      case FolderWatchMode.Polling:
        return translate('folder-watch-mode-pipe.polling');
      default:
        return '';
    }
  }

}
