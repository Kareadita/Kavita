import {inject, Pipe, PipeTransform} from '@angular/core';
import {LibraryService} from "../_services/library.service";
import {Observable} from "rxjs";

@Pipe({
  name: 'libraryName',
  standalone: true
})
export class LibraryNamePipe implements PipeTransform {
  private readonly libraryService = inject(LibraryService);

  transform(libraryId: number): Observable<string> {
    return this.libraryService.getLibraryName(libraryId);
  }

}
