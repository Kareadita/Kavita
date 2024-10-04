import { Pipe, PipeTransform } from '@angular/core';
import {translate} from "@jsverse/transloco";
import {WritingStyle} from "../_models/preferences/writing-style";
import {ScalingOption} from "../_models/preferences/scaling-option";

@Pipe({
  name: 'writingStyle',
  standalone: true
})
export class WritingStylePipe implements PipeTransform {

  transform(value: WritingStyle): string {
    const v = parseInt(value + '', 10) as WritingStyle;
    switch (v) {
      case WritingStyle.Horizontal: return translate('preferences.horizontal');
      case WritingStyle.Vertical: return translate('preferences.vertical');
    }
  }

}
