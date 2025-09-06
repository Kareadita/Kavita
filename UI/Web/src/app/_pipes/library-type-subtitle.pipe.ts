import {Pipe, PipeTransform} from '@angular/core';
import {LibraryType} from "../_models/library/library";
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'libraryTypeSubtitle'
})
export class LibraryTypeSubtitlePipe implements PipeTransform {

  transform(value: LibraryType | null | undefined): string {
    if (value === null || value === undefined) return '';

    switch (value) {
      case LibraryType.Manga:
        return translate('library-type-subtitle-pipe.manga');
      case LibraryType.Comic:
        return translate('library-type-subtitle-pipe.comic');
      case LibraryType.Book:
        return translate('library-type-subtitle-pipe.book');
      case LibraryType.Images:
        return translate('library-type-subtitle-pipe.image');
      case LibraryType.LightNovel:
        return translate('library-type-subtitle-pipe.lightNovel');
      case LibraryType.ComicVine:
        return translate('library-type-subtitle-pipe.comicVine');

    }
  }

}
