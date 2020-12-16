import { Component, OnInit } from '@angular/core';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { DirectoryPickerComponent, DirectoryPickerResult } from 'src/app/directory-picker/directory-picker.component';
import { Library } from 'src/app/_models/library';
import { LibraryService } from 'src/app/_services/library.service';
import { LibraryEditorModalComponent } from '../_modals/library-editor-modal/library-editor-modal.component';

@Component({
  selector: 'app-manage-library',
  templateUrl: './manage-library.component.html',
  styleUrls: ['./manage-library.component.scss']
})
export class ManageLibraryComponent implements OnInit {

  libraries: Library[] = [];

  constructor(private modalService: NgbModal, private libraryService: LibraryService) { }

  ngOnInit(): void {

    this.libraryService.getLibraries().subscribe(libraries => {
      this.libraries = libraries;
    });

  }

  addLibrary() {
    const modalRef = this.modalService.open(LibraryEditorModalComponent);
  }

  addFolder(library: string) {

    const modalRef = this.modalService.open(DirectoryPickerComponent);
    //modalRef.componentInstance.name = 'World';
    modalRef.closed.subscribe((closeResult: DirectoryPickerResult) => {
      console.log('Closed Result', closeResult);
      if (closeResult.success) {
        console.log('Add folder path to Library');
      }
    });

  }

}
