import { Pipe, PipeTransform } from '@angular/core';
import { MangaFormat } from '../_models/manga-format';

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
        return 'EPUB';
      case MangaFormat.ARCHIVE:
        return 'Archive';
      case MangaFormat.IMAGE:
        return 'Image';
      case MangaFormat.PDF:
        return 'PDF';
      case MangaFormat.UNKNOWN:
        return 'Unknown';
    }
  }

}
