import { Component, OnInit } from '@angular/core';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
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
  createLibraryToggle = false;
  loading = false;

  constructor(private modalService: NgbModal, private libraryService: LibraryService, private toastr: ToastrService) { }

  ngOnInit(): void {
    this.getLibraries();
  }

  getLibraries() {
    this.loading = true;
    this.libraryService.getLibraries().subscribe(libraries => {
      this.libraries = libraries;
      this.loading = false;
    });
  }

  editLibrary(library: Library) {
    const modalRef = this.modalService.open(LibraryEditorModalComponent);
    console.log('component instance: ', modalRef.componentInstance);
    modalRef.componentInstance.library = library;
    modalRef.closed.subscribe(refresh => {
      if (refresh) {
        this.getLibraries();
      }
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
        this.toastr.success('Library has been removed');
      });
    }
  }

  scanLibrary(library: Library) {
    this.libraryService.scan(library.id).subscribe(() => {
      this.toastr.success('A scan has been queued for ' + library.name);
    });
  }

}
