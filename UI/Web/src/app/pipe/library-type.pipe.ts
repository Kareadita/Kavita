import { Pipe, PipeTransform } from '@angular/core';
import { LibraryType } from '../_models/library';

/**
 * Returns the name of the LibraryType
 */
@Pipe({
  name: 'libraryType',
  standalone: true
})
export class LibraryTypePipe implements PipeTransform {

  transform(libraryType: LibraryType): string {
    switch(libraryType) {
      case LibraryType.Book:
        return 'Book';
      case LibraryType.Comic:
        return 'Comic';
      case LibraryType.Manga:
        return 'Manga';
    }
  }

}
