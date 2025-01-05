import { Pipe, PipeTransform } from '@angular/core';
import {PlusMediaFormat} from "../_models/series-detail/external-series-detail";
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'plusMediaFormat',
  standalone: true
})
export class PlusMediaFormatPipe implements PipeTransform {

  transform(value: PlusMediaFormat): string {
    switch (value) {
      case PlusMediaFormat.Manga:
        return translate('library-type-pipe.manga');
      case PlusMediaFormat.Comic:
        return translate('library-type-pipe.comic');
      case PlusMediaFormat.LightNovel:
        return translate('library-type-pipe.lightNovel');
      case PlusMediaFormat.Book:
        return translate('library-type-pipe.book');

    }
  }

}
