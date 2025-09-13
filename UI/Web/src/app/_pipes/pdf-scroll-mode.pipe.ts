import { Pipe, PipeTransform } from '@angular/core';
import {translate} from "@jsverse/transloco";
import {PdfScrollMode} from "../_models/preferences/pdf-scroll-mode";

@Pipe({
  name: 'pdfScrollMode',
  standalone: true
})
export class PdfScrollModePipe implements PipeTransform {

  transform(value: PdfScrollMode): string {
    const v = parseInt(value + '', 10) as PdfScrollMode;
    switch (v) {
      case PdfScrollMode.Wrapped: return translate('preferences.pdf-multiple');
      case PdfScrollMode.Page: return translate('preferences.pdf-page');
      case PdfScrollMode.Horizontal: return translate('preferences.pdf-horizontal');
      case PdfScrollMode.Vertical: return translate('preferences.pdf-vertical');
    }
  }

}
