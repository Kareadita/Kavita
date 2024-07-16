import { Pipe, PipeTransform } from '@angular/core';
import {translate} from "@ngneat/transloco";
import {ScalingOption} from "../_models/preferences/scaling-option";

@Pipe({
  name: 'scalingOption',
  standalone: true
})
export class ScalingOptionPipe implements PipeTransform {

  transform(value: ScalingOption): string {
    switch (value) {
      case ScalingOption.Automatic: return translate('preferences.automatic');
      case ScalingOption.FitToHeight: return translate('preferences.fit-to-height');
      case ScalingOption.FitToWidth: return translate('preferences.fit-to-width');
      case ScalingOption.Original: return translate('preferences.original');
    }
  }

}
