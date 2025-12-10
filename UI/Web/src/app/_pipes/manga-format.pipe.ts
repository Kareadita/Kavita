import { Pipe, PipeTransform, inject } from '@angular/core';
import { MangaFormat } from '../_models/manga-format';
import {translate, TranslocoService} from "@jsverse/transloco";

/**
 * Returns the string name for the format
 */
@Pipe({
  name: 'mangaFormat',
  standalone: true
})
export class MangaFormatPipe implements PipeTransform {

  transform(format: MangaFormat): string {
    switch (format) {
      case MangaFormat.EPUB:
        return translate('manga-format-pipe.epub');
      case MangaFormat.ARCHIVE:
        return translate('manga-format-pipe.archive');
      case MangaFormat.IMAGE:
        return translate('manga-format-pipe.image');
      case MangaFormat.PDF:
        return translate('manga-format-pipe.pdf');
      case MangaFormat.UNKNOWN:
        return translate('manga-format-pipe.unknown');
      default:
        return '';
    }
  }

}
