import { Pipe, PipeTransform } from '@angular/core';
import {ReaderMode} from "../_models/preferences/reader-mode";
import {translate} from "@jsverse/transloco";
import {ScalingOption} from "../_models/preferences/scaling-option";

@Pipe({
  name: 'readerMode',
  standalone: true
})
export class ReaderModePipe implements PipeTransform {

  transform(value: ReaderMode): string {
    const v = parseInt(value + '', 10) as ReaderMode;
    switch (v) {
      case ReaderMode.UpDown: return translate('preferences.up-to-down');
      case ReaderMode.Webtoon: return translate('preferences.webtoon');
      case ReaderMode.LeftRight: return translate('preferences.left-to-right');
    }
  }

}
