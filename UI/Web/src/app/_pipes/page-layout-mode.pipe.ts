import { Pipe, PipeTransform } from '@angular/core';
import {PageLayoutMode} from "../_models/page-layout-mode";
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'pageLayoutMode',
  standalone: true
})
export class PageLayoutModePipe implements PipeTransform {

  transform(value: PageLayoutMode): string {
    switch (value) {
      case PageLayoutMode.Cards: return translate('preferences.cards');
      case PageLayoutMode.List: return translate('preferences.list');
    }
  }

}
