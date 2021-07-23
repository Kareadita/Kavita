import { Component, OnDestroy, OnInit } from '@angular/core';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { Subject } from 'rxjs';
import { take, takeUntil } from 'rxjs/operators';
import { ConfirmService } from 'src/app/shared/confirm.service';
import { Library, LibraryType } from 'src/app/_models/library';
import { LibraryService } from 'src/app/_services/library.service';
import { LibraryEditorModalComponent } from '../_modals/library-editor-modal/library-editor-modal.component';

@Component({
  selector: 'app-manage-library',
  templateUrl: './manage-library.component.html',
  styleUrls: ['./manage-library.component.scss']
})
export class ManageLibraryComponent implements OnInit, OnDestroy {

  libraries: Library[] = [];
  createLibraryToggle = false;
  loading = false;
  /**
   * If a deletion is in progress for a library
   */
  deletionInProgress: boolean = false;

  private readonly onDestroy = new Subject<void>();

  constructor(private modalService: NgbModal, private libraryService: LibraryService, private toastr: ToastrService, private confirmService: ConfirmService) { }

  ngOnInit(): void {
    this.getLibraries();
  }

  ngOnDestroy() {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  getLibraries() {
    this.loading = true;
    this.libraryService.getLibraries().pipe(take(1)).subscribe(libraries => {
      this.libraries = libraries;
      this.loading = false;
    });
  }

  editLibrary(library: Library) {
    const modalRef = this.modalService.open(LibraryEditorModalComponent);
    modalRef.componentInstance.library = library;
    modalRef.closed.pipe(takeUntil(this.onDestroy)).subscribe(refresh => {
      if (refresh) {
        this.getLibraries();
      }
    });
  }

  addLibrary() {
    const modalRef = this.modalService.open(LibraryEditorModalComponent);
    modalRef.closed.pipe(takeUntil(this.onDestroy)).subscribe(refresh => {
      if (refresh) {
        this.getLibraries();
      }
    });
  }

  async deleteLibrary(library: Library) {
    if (await this.confirmService.confirm('Are you sure you want to delete this library? You cannot undo this action.')) {
      this.deletionInProgress = true;
      this.libraryService.delete(library.id).pipe(take(1)).subscribe(() => {
        this.deletionInProgress = false;
        this.getLibraries();
        this.toastr.success('Library has been removed');
      });
    }
  }

  scanLibrary(library: Library) {
    this.libraryService.scan(library.id).pipe(take(1)).subscribe(() => {
      this.toastr.success('A scan has been queued for ' + library.name);
    });
  }

  libraryType(libraryType: LibraryType) {
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
