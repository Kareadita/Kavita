import { Pipe, PipeTransform } from '@angular/core';
import {translate} from "@jsverse/transloco";
import {ScalingOption} from "../_models/preferences/scaling-option";
import {ReadingDirection} from "../_models/preferences/reading-direction";

@Pipe({
  name: 'scalingOption',
  standalone: true
})
export class ScalingOptionPipe implements PipeTransform {

  transform(value: ScalingOption): string {
    const v = parseInt(value + '', 10) as ScalingOption;
    switch (v) {
      case ScalingOption.Automatic: return translate('preferences.automatic');
      case ScalingOption.FitToHeight: return translate('preferences.fit-to-height');
      case ScalingOption.FitToWidth: return translate('preferences.fit-to-width');
      case ScalingOption.Original: return translate('preferences.original');
    }
  }

}
