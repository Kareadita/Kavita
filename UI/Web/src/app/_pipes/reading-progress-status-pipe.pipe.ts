import {Pipe, PipeTransform} from '@angular/core';
import {ReadingProgressStatus} from "../_models/series-detail/reading-progress";
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'readingProgressStatusPipe'
})
export class ReadingProgressStatusPipePipe implements PipeTransform {

  transform(value: ReadingProgressStatus, incognito: boolean = false): string {
    const suffix = incognito ? '-incognito' : '';

    switch (value) {
      case ReadingProgressStatus.NoProgress:
        return translate('reading-progress-status-pipe.no-progress' + suffix);
      case ReadingProgressStatus.Progress:
        return translate('reading-progress-status-pipe.progress' + suffix);
      case ReadingProgressStatus.FullyRead:
        return translate('reading-progress-status-pipe.full-read' + suffix);
    }
  }

}
