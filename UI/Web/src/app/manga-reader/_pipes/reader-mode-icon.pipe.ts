import { Pipe, PipeTransform } from '@angular/core';
import { ReaderMode } from 'src/app/_models/preferences/reader-mode';

@Pipe({
  name: 'readerModeIcon',
  standalone: true,
})
export class ReaderModeIconPipe implements PipeTransform {

  transform(readerMode: ReaderMode): string {
    switch(readerMode) {
      case ReaderMode.LeftRight:
        return 'fa-exchange-alt';
      case ReaderMode.UpDown:
        return 'fa-exchange-alt fa-rotate-90';
      case ReaderMode.Webtoon:
        return 'fa-arrows-alt-v';
      default:
        return '';
    }
  }

}
