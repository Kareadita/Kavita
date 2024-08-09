import { Pipe, PipeTransform } from '@angular/core';
import {translate} from "@ngneat/transloco";
import {BookPageLayoutMode} from "../_models/readers/book-page-layout-mode";

@Pipe({
  name: 'bookPageLayoutMode',
  standalone: true
})
export class BookPageLayoutModePipe implements PipeTransform {

  transform(value: BookPageLayoutMode): string {
    switch (value) {
      case BookPageLayoutMode.Column1: return translate('preferences.1-column');
      case BookPageLayoutMode.Column2: return translate('preferences.2-column');
      case BookPageLayoutMode.Default: return translate('preferences.scroll');
    }
  }

}
