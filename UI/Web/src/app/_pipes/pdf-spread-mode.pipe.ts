import { Pipe, PipeTransform } from '@angular/core';
import {PdfSpreadMode} from "../_models/preferences/pdf-spread-mode";
import {translate} from "@ngneat/transloco";

@Pipe({
  name: 'pdfSpreadMode',
  standalone: true
})
export class PdfSpreadModePipe implements PipeTransform {

  transform(value: PdfSpreadMode): string {
    switch (value) {
      case PdfSpreadMode.None: return translate('preferences.pdf-none');
      case PdfSpreadMode.Odd: return translate('preferences.pdf-odd');
      case PdfSpreadMode.Even: return translate('preferences.pdf-even');
    }
  }

}
