import { Component, OnInit } from '@angular/core';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { Library } from 'src/app/_models/library';
import { LibraryService } from 'src/app/_services/library.service';
import { DirectoryPickerComponent, DirectoryPickerResult } from '../_modals/directory-picker/directory-picker.component';
import { LibraryEditorModalComponent } from '../_modals/library-editor-modal/library-editor-modal.component';

@Component({
  selector: 'app-manage-library',
  templateUrl: './manage-library.component.html',
  styleUrls: ['./manage-library.component.scss']
})
export class ManageLibraryComponent implements OnInit {

  libraries: Library[] = [];
  createLibraryToggle = false;

  constructor(private modalService: NgbModal, private libraryService: LibraryService) { }

  ngOnInit(): void {

    this.getLibraries();

  }

  getLibraries() {
    this.libraryService.getLibraries().subscribe(libraries => {
      this.libraries = libraries;
    });
  }

  addLibrary() {
    const modalRef = this.modalService.open(LibraryEditorModalComponent);
    modalRef.closed.subscribe(refresh => {
      if (refresh) {
        this.getLibraries();
      }
    });
  }

  deleteLibrary(library: Library) {
    if (confirm('Are you sure you want to delete this library? You cannot undo this action.')) {
      this.libraryService.delete(library.id).subscribe(() => {
        this.getLibraries();
      });
    }
  }

}
