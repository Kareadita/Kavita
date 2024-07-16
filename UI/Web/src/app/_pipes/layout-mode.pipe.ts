import { Pipe, PipeTransform } from '@angular/core';
import {translate} from "@ngneat/transloco";
import {LayoutMode} from "../manga-reader/_models/layout-mode";

@Pipe({
  name: 'layoutMode',
  standalone: true
})
export class LayoutModePipe implements PipeTransform {

  transform(value: LayoutMode): string {
    switch (value) {
      case LayoutMode.Single: return translate('preferences.single');
      case LayoutMode.Double: return translate('preferences.double');
      case LayoutMode.DoubleReversed: return translate('preferences.double-manga');
      case LayoutMode.DoubleNoCover: return translate('preferences.double-no-cover');
    }
  }

}
