import { Pipe, PipeTransform } from '@angular/core';
import {ReaderMode} from "../_models/preferences/reader-mode";
import {translate} from "@ngneat/transloco";

@Pipe({
  name: 'readerMode',
  standalone: true
})
export class ReaderModePipe implements PipeTransform {

  transform(value: ReaderMode): string {
    switch (value) {
      case ReaderMode.UpDown: return translate('preferences.up-down');
      case ReaderMode.Webtoon: return translate('preferences.webtoon');
      case ReaderMode.LeftRight: return translate('preferences.left-to-right');
    }
  }

}
