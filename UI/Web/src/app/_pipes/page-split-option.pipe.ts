import { Pipe, PipeTransform } from '@angular/core';
import {translate} from "@jsverse/transloco";
import {PageSplitOption} from "../_models/preferences/page-split-option";
import {ScalingOption} from "../_models/preferences/scaling-option";

@Pipe({
  name: 'pageSplitOption',
  standalone: true
})
export class PageSplitOptionPipe implements PipeTransform {

  transform(value: PageSplitOption): string {
    const v = parseInt(value + '', 10) as PageSplitOption;
    switch (v) {
      case PageSplitOption.FitSplit: return translate('preferences.fit-to-screen');
      case PageSplitOption.NoSplit: return translate('preferences.no-split');
      case PageSplitOption.SplitLeftToRight: return translate('preferences.split-left-to-right');
      case PageSplitOption.SplitRightToLeft: return translate('preferences.split-right-to-left');
    }
  }

}
