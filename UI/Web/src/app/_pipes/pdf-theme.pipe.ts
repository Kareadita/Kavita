import { Pipe, PipeTransform } from '@angular/core';
import {PdfTheme} from "../_models/preferences/pdf-theme";
import {translate} from "@jsverse/transloco";
import {ScalingOption} from "../_models/preferences/scaling-option";

@Pipe({
  name: 'pdfTheme',
  standalone: true
})
export class PdfThemePipe implements PipeTransform {

  transform(value: PdfTheme): string {
    const v = parseInt(value + '', 10) as PdfTheme;
    switch (v) {
      case PdfTheme.Dark: return translate('preferences.pdf-dark');
      case PdfTheme.Light: return translate('preferences.pdf-light');
    }
  }

}
