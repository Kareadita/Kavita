import {Pipe, PipeTransform} from '@angular/core';
import {ReadingProgressStatus} from "../_models/series-detail/reading-progress";

@Pipe({
  name: 'readingProgressIconPipe',
})
export class ReadingProgressIconPipePipe implements PipeTransform {

  transform(value: ReadingProgressStatus | undefined): string {
    if (value === undefined) return 'fa fa-book';

    switch (value) {
      case ReadingProgressStatus.NoProgress:
        return 'fa fa-book';
      case ReadingProgressStatus.Progress:
        return 'fa fa-book-open';
      case ReadingProgressStatus.FullyRead:
        return 'fa fa-book';

    }
  }

}
