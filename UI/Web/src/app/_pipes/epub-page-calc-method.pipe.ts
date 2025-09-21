import {Pipe, PipeTransform} from '@angular/core';
import {EpubPageCalculationMethod} from "../_models/readers/epub-page-calculation-method";
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'epubPageCalcMethod'
})
export class EpubPageCalcMethodPipe implements PipeTransform {

  transform(value: EpubPageCalculationMethod) {
    switch (value) {
      case EpubPageCalculationMethod.Default:
        return translate('epub-page-calc-method-pipe.default');
      case EpubPageCalculationMethod.Calculation1:
        return translate('epub-page-calc-method-pipe.calc1');

    }
  }

}
