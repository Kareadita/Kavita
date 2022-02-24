import { Component, OnDestroy, OnInit } from '@angular/core';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { Subject } from 'rxjs';
import { take, takeUntil, takeWhile } from 'rxjs/operators';
import { ConfirmService } from 'src/app/shared/confirm.service';
import { NotificationProgressEvent } from 'src/app/_models/events/notification-progress-event';
import { ProgressEvent } from 'src/app/_models/events/progress-event';
import { Library, LibraryType } from 'src/app/_models/library';
import { LibraryService } from 'src/app/_services/library.service';
import { EVENTS, Message, MessageHubService } from 'src/app/_services/message-hub.service';
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
  scanInProgress: {[key: number]: {progress: boolean, timestamp?: string}} = {};
  libraryTrackBy = (index: number, item: Library) => `${item.name}_${item.lastScanned}_${item.type}_${item.folders.length}`;

  private readonly onDestroy = new Subject<void>();

  constructor(private modalService: NgbModal, private libraryService: LibraryService, 
    private toastr: ToastrService, private confirmService: ConfirmService,
    private hubService: MessageHubService) { }

  ngOnInit(): void {
    this.getLibraries();

    // when a progress event comes in, show it on the UI next to library
    this.hubService.messages$.pipe(takeUntil(this.onDestroy), takeWhile(event => event.event === EVENTS.NotificationProgress))
      .subscribe((event: Message<NotificationProgressEvent>) => {
      if (event.event !== EVENTS.NotificationProgress && (event.payload as NotificationProgressEvent).name === EVENTS.ScanSeries) return;

      console.log('scan event: ', event.payload);
      // TODO: Refactor this to use EventyType on NotificationProgress interface rather than float comparison
      
      const scanEvent = event.payload.body as ProgressEvent;
      this.scanInProgress[scanEvent.libraryId] = {progress: scanEvent.progress !== 1};
      if (scanEvent.progress === 0) {
        this.scanInProgress[scanEvent.libraryId].timestamp = scanEvent.eventTime;
      }
      
      if (this.scanInProgress[scanEvent.libraryId].progress === false && (scanEvent.progress === 1 || event.payload.eventType === 'ended')) {
        this.libraryService.getLibraries().pipe(take(1)).subscribe(libraries => {
          const newLibrary = libraries.find(lib => lib.id === scanEvent.libraryId);
          const existingLibrary = this.libraries.find(lib => lib.id === scanEvent.libraryId);
          if (existingLibrary !== undefined) {
            existingLibrary.lastScanned = newLibrary?.lastScanned || existingLibrary.lastScanned;
          }
        });
      }

    });
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
      this.toastr.info('A scan has been queued for ' + library.name);
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
