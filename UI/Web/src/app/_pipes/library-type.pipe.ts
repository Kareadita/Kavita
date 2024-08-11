import {inject, Pipe, PipeTransform} from '@angular/core';
import { LibraryType } from '../_models/library/library';
import {TranslocoService} from "@jsverse/transloco";

/**
 * Returns the name of the LibraryType
 */
@Pipe({
  name: 'libraryType',
  standalone: true
})
export class LibraryTypePipe implements PipeTransform {

  translocoService = inject(TranslocoService);
  transform(libraryType: LibraryType): string {
    switch (libraryType) {
      case LibraryType.Book:
        return this.translocoService.translate('library-type-pipe.book');
      case LibraryType.Comic:
        return this.translocoService.translate('library-type-pipe.comic');
      case LibraryType.ComicVine:
        return this.translocoService.translate('library-type-pipe.comicVine');
      case LibraryType.Images:
        return this.translocoService.translate('library-type-pipe.image');
      case LibraryType.Manga:
        return this.translocoService.translate('library-type-pipe.manga');
      case LibraryType.LightNovel:
        return this.translocoService.translate('library-type-pipe.lightNovel');
      default:
        return '';
    }
  }

}
