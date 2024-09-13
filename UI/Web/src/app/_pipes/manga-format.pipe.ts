import {Pipe, PipeTransform} from '@angular/core';
import { MangaFormat } from '../_models/manga-format';
import {TranslocoService} from "@jsverse/transloco";

/**
 * Returns the string name for the format
 */
@Pipe({
  name: 'mangaFormat',
  standalone: true
})
export class MangaFormatPipe implements PipeTransform {

  constructor(private translocoService: TranslocoService) {}

  transform(format: MangaFormat): string {
    switch (format) {
      case MangaFormat.EPUB:
        return this.translocoService.translate('manga-format-pipe.epub');
      case MangaFormat.ARCHIVE:
        return this.translocoService.translate('manga-format-pipe.archive');
      case MangaFormat.IMAGE:
        return this.translocoService.translate('manga-format-pipe.image');
      case MangaFormat.PDF:
        return this.translocoService.translate('manga-format-pipe.pdf');
      case MangaFormat.UNKNOWN:
        return this.translocoService.translate('manga-format-pipe.unknown');
      default:
        return '';
    }
  }

}
