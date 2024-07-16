import { Pipe, PipeTransform } from '@angular/core';
import {translate} from "@ngneat/transloco";
import {WritingStyle} from "../_models/preferences/writing-style";

@Pipe({
  name: 'writingStyle',
  standalone: true
})
export class WritingStylePipe implements PipeTransform {

  transform(value: WritingStyle): string {
    switch (value) {
      case WritingStyle.Horizontal: return translate('preferences.horizontal');
      case WritingStyle.Vertical: return translate('preferences.vertical');
    }
  }

}
