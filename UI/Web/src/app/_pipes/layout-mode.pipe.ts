import { Pipe, PipeTransform } from '@angular/core';
import {translate} from "@jsverse/transloco";
import {LayoutMode} from "../manga-reader/_models/layout-mode";
import {ScalingOption} from "../_models/preferences/scaling-option";

@Pipe({
  name: 'layoutMode',
  standalone: true
})
export class LayoutModePipe implements PipeTransform {

  transform(value: LayoutMode): string {
    const v = parseInt(value + '', 10) as LayoutMode;
    switch (v) {
      case LayoutMode.Single: return translate('preferences.single');
      case LayoutMode.Double: return translate('preferences.double');
      case LayoutMode.DoubleReversed: return translate('preferences.double-manga');
      case LayoutMode.DoubleNoCover: return translate('preferences.double-no-cover');
    }
  }

}
