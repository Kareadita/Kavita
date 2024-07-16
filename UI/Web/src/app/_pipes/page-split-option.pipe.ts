import { Pipe, PipeTransform } from '@angular/core';
import {translate} from "@ngneat/transloco";
import {PageSplitOption} from "../_models/preferences/page-split-option";

@Pipe({
  name: 'pageSplitOption',
  standalone: true
})
export class PageSplitOptionPipe implements PipeTransform {

  transform(value: PageSplitOption): string {
    switch (value) {
      case PageSplitOption.FitSplit: return translate('preferences.fit-to-screen');
      case PageSplitOption.NoSplit: return translate('preferences.no-split');
      case PageSplitOption.SplitLeftToRight: return translate('preferences.split-left-to-right');
      case PageSplitOption.SplitRightToLeft: return translate('preferences.split-right-to-left');
    }
  }

}
