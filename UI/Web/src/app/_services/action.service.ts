import { Injectable } from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { Library } from '../_models/library';
import { LibraryService } from './library.service';

export type LibraryActionCallback = (library: Partial<Library>) => void;

@Injectable({
  providedIn: 'root'
})
export class ActionService {

  /**
   * Responsible for executing actions
   */
  constructor(private libraryService: LibraryService, private toastr: ToastrService) { }

  scanLibrary(library: Partial<Library>, callback?: LibraryActionCallback) {
    if (!library.hasOwnProperty('id') || library.id === undefined) {
      return;
    }
    this.libraryService.scan(library?.id).subscribe((res: any) => {
      this.toastr.success('Scan started for ' + library.name);
      if (callback) {
        callback(library);
      }
    });
  }

  refreshMetadata(library: Partial<Library>, callback?: LibraryActionCallback) {
    if (!library.hasOwnProperty('id') || library.id === undefined) {
      return;
    }
    
    this.libraryService.refreshMetadata(library?.id).subscribe((res: any) => {
      this.toastr.success('Scan started for ' + library.name);
      if (callback) {
        callback(library);
      }
    });
  }
}
