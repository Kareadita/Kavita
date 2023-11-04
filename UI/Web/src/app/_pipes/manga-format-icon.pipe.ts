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
        return 'fa-book';
      case MangaFormat.ARCHIVE:
        return 'fa-file-archive';
      case MangaFormat.IMAGE:
        return 'fa-image';
      case MangaFormat.PDF:
        return 'fa-file-pdf';
      case MangaFormat.UNKNOWN:
        return 'fa-question';
    }
  }

}
