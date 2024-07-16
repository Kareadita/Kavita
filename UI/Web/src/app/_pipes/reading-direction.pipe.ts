import { Pipe, PipeTransform } from '@angular/core';
import {ReadingDirection} from "../_models/preferences/reading-direction";
import {translate} from "@ngneat/transloco";

@Pipe({
  name: 'readingDirection',
  standalone: true
})
export class ReadingDirectionPipe implements PipeTransform {

  transform(value: ReadingDirection): string {
    switch (value) {
      case ReadingDirection.LeftToRight: return translate('preferences.left-to-right');
      case ReadingDirection.RightToLeft: return translate('preferences.right-to-right');
    }
  }

}
