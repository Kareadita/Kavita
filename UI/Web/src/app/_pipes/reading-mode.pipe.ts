import { Pipe, PipeTransform } from '@angular/core';
import {ReaderMode} from "../_models/preferences/reader-mode";
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'readerMode',
  standalone: true
})
export class ReaderModePipe implements PipeTransform {

  transform(value: ReaderMode): string {
    switch (value) {
      case ReaderMode.UpDown: return translate('preferences.up-to-down');
      case ReaderMode.Webtoon: return translate('preferences.webtoon');
      case ReaderMode.LeftRight: return translate('preferences.left-to-right');
    }
  }

}
