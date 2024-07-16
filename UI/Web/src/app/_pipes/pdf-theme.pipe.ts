import { Pipe, PipeTransform } from '@angular/core';
import {PdfTheme} from "../_models/preferences/pdf-theme";
import {translate} from "@ngneat/transloco";

@Pipe({
  name: 'pdfTheme',
  standalone: true
})
export class PdfThemePipe implements PipeTransform {

  transform(value: PdfTheme): string {
    switch (value) {
      case PdfTheme.Dark: return translate('preferences.pdf-dark');
      case PdfTheme.Light: return translate('preferences.pdf-light');
    }
  }

}
