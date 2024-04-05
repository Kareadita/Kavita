import { Pipe, PipeTransform } from '@angular/core';
import { MangaFormat } from '../_models/manga-format';

/**
 * Returns the icon class representing the format
 */
@Pipe({
  name: 'mangaFormatIcon',
  standalone: true
})
export class MangaFormatIconPipe implements PipeTransform {

  transform(format: MangaFormat): string {
    switch (format) {
      case MangaFormat.EPUB:
        return 'fa fa-book';
      case MangaFormat.ARCHIVE:
        return 'fa-solid fa-file-zipper';
      case MangaFormat.IMAGE:
        return 'fa-solid fa-file-image';
      case MangaFormat.PDF:
        return 'fa-solid fa-file-pdf';
      case MangaFormat.UNKNOWN:
        return 'fa-solid fa-file-circle-question';
    }
  }

}
