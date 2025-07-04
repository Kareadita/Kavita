import {Pipe, PipeTransform} from '@angular/core';
import {BookPageLayoutMode} from "../../_models/readers/book-page-layout-mode";

@Pipe({
  name: 'columnLayoutClass'
})
export class ColumnLayoutClassPipe implements PipeTransform {

  transform(value: BookPageLayoutMode): string {
    switch (value) {
      case BookPageLayoutMode.Default:
        return '';
      case BookPageLayoutMode.Column1:
        return 'column-layout-1';
      case BookPageLayoutMode.Column2:
        return 'column-layout-2';
    }
  }

}
