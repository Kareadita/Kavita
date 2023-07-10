import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  OnInit
} from '@angular/core';
import { NgbModal, NgbTooltip } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { distinctUntilChanged, filter, take } from 'rxjs/operators';
import { ConfirmService } from 'src/app/shared/confirm.service';
import { LibrarySettingsModalComponent } from 'src/app/sidenav/_modals/library-settings-modal/library-settings-modal.component';
import { NotificationProgressEvent } from 'src/app/_models/events/notification-progress-event';
import { ScanSeriesEvent } from 'src/app/_models/events/scan-series-event';
import { Library } from 'src/app/_models/library';
import { LibraryService } from 'src/app/_services/library.service';
import { EVENTS, Message, MessageHubService } from 'src/app/_services/message-hub.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { SentenceCasePipe } from '../../pipe/sentence-case.pipe';
import { TimeAgoPipe } from '../../pipe/time-ago.pipe';
import { LibraryTypePipe } from '../../pipe/library-type.pipe';
import { RouterLink } from '@angular/router';
import { NgFor, NgIf } from '@angular/common';

@Component({
    selector: 'app-manage-library',
    templateUrl: './manage-library.component.html',
    styleUrls: ['./manage-library.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
    imports: [NgFor, RouterLink, NgbTooltip, NgIf, LibraryTypePipe, TimeAgoPipe, SentenceCasePipe]
})
export class ManageLibraryComponent implements OnInit {

  libraries: Library[] = [];
  loading = false;
  /**
   * If a deletion is in progress for a library
   */
  deletionInProgress: boolean = false;
  libraryTrackBy = (index: number, item: Library) => `${item.name}_${item.lastScanned}_${item.type}_${item.folders.length}`;
  private readonly destroyRef = inject(DestroyRef);

  constructor(private modalService: NgbModal, private libraryService: LibraryService,
    private toastr: ToastrService, private confirmService: ConfirmService,
    private hubService: MessageHubService, private readonly cdRef: ChangeDetectorRef) { }

  ngOnInit(): void {
    this.getLibraries();

    // when a progress event comes in, show it on the UI next to library
    this.hubService.messages$.pipe(takeUntilDestroyed(this.destroyRef),
      filter(event => event.event === EVENTS.ScanSeries || event.event === EVENTS.NotificationProgress),
      distinctUntilChanged((prev: Message<ScanSeriesEvent | NotificationProgressEvent>, curr: Message<ScanSeriesEvent | NotificationProgressEvent>) =>
        this.hasMessageChanged(prev, curr)))
      .subscribe((event: Message<ScanSeriesEvent | NotificationProgressEvent>) => {
        let libId = 0;
        if (event.event === EVENTS.ScanSeries) {
          libId = (event.payload as ScanSeriesEvent).libraryId;
        } else {
          if ((event.payload as NotificationProgressEvent).body.hasOwnProperty('libraryId')) {
            libId = (event.payload as NotificationProgressEvent).body.libraryId;
          }
        }

        this.libraryService.getLibraries().pipe(take(1)).subscribe(libraries => {
          const newLibrary = libraries.find(lib => lib.id === libId);
          const existingLibrary = this.libraries.find(lib => lib.id === libId);
          if (existingLibrary !== undefined) {
            existingLibrary.lastScanned = newLibrary?.lastScanned || existingLibrary.lastScanned;
            this.cdRef.markForCheck();
          }
        });
    });
  }

  hasMessageChanged(prev: Message<ScanSeriesEvent | NotificationProgressEvent>, curr: Message<ScanSeriesEvent | NotificationProgressEvent>) {
    if (curr.event !== prev.event) return true;
    if (curr.event === EVENTS.ScanSeries) {
      return (prev.payload as ScanSeriesEvent).libraryId === (curr.payload as ScanSeriesEvent).libraryId;
    }
    if (curr.event === EVENTS.NotificationProgress) {
      return (prev.payload as NotificationProgressEvent).eventType != (curr.payload as NotificationProgressEvent).eventType;
    }
    return false;
  }

  getLibraries() {
    this.loading = true;
    this.cdRef.markForCheck();
    this.libraryService.getLibraries().pipe(take(1), takeUntilDestroyed(this.destroyRef)).subscribe(libraries => {
      this.libraries = [...libraries];
      this.loading = false;
      this.cdRef.markForCheck();
    });
  }

  editLibrary(library: Library) {
    const modalRef = this.modalService.open(LibrarySettingsModalComponent, {  size: 'xl' });
    modalRef.componentInstance.library = library;
    modalRef.closed.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(refresh => {
      if (refresh) {
        this.getLibraries();
      }
    });
  }

  addLibrary() {
    const modalRef = this.modalService.open(LibrarySettingsModalComponent, {  size: 'xl' });
    modalRef.closed.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(refresh => {
      if (refresh) {
        this.getLibraries();
      }
    });
  }

  async deleteLibrary(library: Library) {
    if (await this.confirmService.confirm('Are you sure you want to delete the ' + library.name + ' library? You cannot undo this action.')) {
      this.deletionInProgress = true;
      this.libraryService.delete(library.id).pipe(take(1)).subscribe(() => {
        this.deletionInProgress = false;
        this.cdRef.markForCheck();
        this.getLibraries();
        this.toastr.success('Library ' + library.name + ' has been removed');
      });
    }
  }

  scanLibrary(library: Library) {
    this.libraryService.scan(library.id).pipe(take(1)).subscribe(() => {
      this.toastr.info('A scan has been queued for ' + library.name);
    });
  }
}
