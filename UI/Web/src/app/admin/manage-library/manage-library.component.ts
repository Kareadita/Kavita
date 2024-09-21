import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef, HostListener,
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
import { Library } from 'src/app/_models/library/library';
import { LibraryService } from 'src/app/_services/library.service';
import { EVENTS, Message, MessageHubService } from 'src/app/_services/message-hub.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { SentenceCasePipe } from '../../_pipes/sentence-case.pipe';
import { TimeAgoPipe } from '../../_pipes/time-ago.pipe';
import { LibraryTypePipe } from '../../_pipes/library-type.pipe';
import { RouterLink } from '@angular/router';
import {translate, TranslocoModule} from "@jsverse/transloco";
import {DefaultDatePipe} from "../../_pipes/default-date.pipe";
import {AsyncPipe, TitleCasePipe} from "@angular/common";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {TagBadgeComponent} from "../../shared/tag-badge/tag-badge.component";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {Breakpoint, KEY_CODES, UtilityService} from "../../shared/_services/utility.service";
import {Action, ActionFactoryService, ActionItem} from "../../_services/action-factory.service";
import {ActionService} from "../../_services/action.service";
import {CardActionablesComponent} from "../../_single-module/card-actionables/card-actionables.component";
import {BehaviorSubject, Observable} from "rxjs";
import {Select2Module} from "ng-select2-component";
import {SelectionModel} from "../../typeahead/_models/selection-model";
import {
  CopySettingsFromLibraryModalComponent
} from "../_modals/copy-settings-from-library-modal/copy-settings-from-library-modal.component";

@Component({
    selector: 'app-manage-library',
    templateUrl: './manage-library.component.html',
    styleUrls: ['./manage-library.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
    imports: [RouterLink, NgbTooltip, LibraryTypePipe, TimeAgoPipe, SentenceCasePipe, TranslocoModule, DefaultDatePipe, AsyncPipe, DefaultValuePipe, LoadingComponent, TagBadgeComponent, TitleCasePipe, UtcToLocalTimePipe, CardActionablesComponent, Select2Module]
})
export class ManageLibraryComponent implements OnInit {

  private readonly libraryService = inject(LibraryService);
  private readonly modalService = inject(NgbModal);
  private readonly toastr = inject(ToastrService);
  private readonly confirmService = inject(ConfirmService);
  private readonly hubService = inject(MessageHubService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly utilityService = inject(UtilityService);
  private readonly actionFactoryService = inject(ActionFactoryService);
  private readonly actionService = inject(ActionService);

  protected readonly Breakpoint = Breakpoint;

  actions = this.actionFactoryService.getLibraryActions(this.handleAction.bind(this));
  bulkActions = this.actionFactoryService.getBulkLibraryActions(this.handleBulkAction.bind(this));
  libraries: Library[] = [];
  loading = false;
  /**
   * If a deletion is in progress for a library
   */
  deletionInProgress: boolean = false;
  useActionableSource = new BehaviorSubject<boolean>(this.utilityService.getActiveBreakpoint() <= Breakpoint.Tablet);
  useActionables$: Observable<boolean> = this.useActionableSource.asObservable();
  selectedLibraries: Array<{selected: boolean, data: Library}> = [];
  selections!: SelectionModel<Library>;
  selectAll: boolean = false;
  bulkMode = false;
  isShiftDown: boolean = false;
  lastSelectedIndex: number | null = null;

  @HostListener('document:keydown.shift', ['$event'])
  handleKeypress(event: KeyboardEvent) {
    this.isShiftDown = true;
  }

  @HostListener('document:keyup.shift', ['$event'])
  handleKeyUp(event: KeyboardEvent) {
    this.isShiftDown = false;
  }


  @HostListener('window:resize', ['$event'])
  @HostListener('window:orientationchange', ['$event'])
  onResize(){
    this.useActionableSource.next(this.utilityService.getActiveBreakpoint() <= Breakpoint.Tablet);
  }

  get hasSomeSelected() {
    return this.selections != null && this.selections.hasSomeSelected();
  }

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
      this.setupSelections();
      this.loading = false;
      this.cdRef.markForCheck();
    });
  }

  editLibrary(library: Library) {
    const modalRef = this.modalService.open(LibrarySettingsModalComponent, {  size: 'xl', fullscreen: 'md' });
    modalRef.componentInstance.library = library;
    modalRef.closed.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(refresh => {
      if (refresh) {
        this.getLibraries();
      }
    });
  }

  addLibrary() {
    const modalRef = this.modalService.open(LibrarySettingsModalComponent, {  size: 'xl', fullscreen: 'md' });
    modalRef.closed.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(refresh => {
      if (refresh) {
        this.getLibraries();
      }
    });
  }

  async deleteLibrary(library: Library) {
    if (await this.confirmService.confirm(translate('toasts.confirm-library-delete', {name: library.name}))) {
      this.deletionInProgress = true;
      this.libraryService.delete(library.id).pipe(take(1)).subscribe(() => {
        this.deletionInProgress = false;
        this.cdRef.markForCheck();
        this.getLibraries();
        this.toastr.success(translate('toasts.library-deleted', {name: library.name}));
      });
    }
  }

  async scanLibrary(library: Library) {
    await this.actionService.scanLibrary(library);
  }

  async handleBulkAction(action: ActionItem<Library>, library : Library | null) {

    // Get Selected libraries
    const selectedLibraries = this.selectedLibraries.filter(l => l.selected).map(l => l.data);

    // Queue up actions in a background queue that runs each job and listens to SignalR events to move to the next (or timeout)
    if (selectedLibraries.length === 0) {
      await this.confirmService.alert(translate('toasts.must-select-library'));
      return;
    }

    switch (action.action) {
      // case(Action.Scan):
      //   await this.actionService.scanLibrary(library);
      //   break;
      // case(Action.RefreshMetadata):
      //   await this.actionService.refreshLibraryMetadata(library);
      //   break;
      // case(Action.GenerateColorScape):
      //   await this.actionService.refreshLibraryMetadata(library, undefined, false, true);
      //   break;
      // case (Action.Delete):
      //   await this.deleteLibrary(library);
      //   break;
      case (Action.CopySettings):
        // Prompt the user
        const ref = this.modalService.open(CopySettingsFromLibraryModalComponent, {size: 'lg', fullscreen: 'md'});
        ref.componentInstance.libraries = this.libraries;
        ref.closed.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((res: Library | null) => {
          if (res === null) return;
          // res will be the library the user chose
          this.bulkMode = true;
          this.cdRef.markForCheck();
        });
        break;
      default:
        break;
    }
  }

  async handleAction(action: ActionItem<Library>, library: Library) {
    switch (action.action) {
      case(Action.Scan):
        await this.actionService.scanLibrary(library);
        break;
      case(Action.RefreshMetadata):
        await this.actionService.refreshLibraryMetadata(library);
        break;
      case(Action.GenerateColorScape):
        await this.actionService.refreshLibraryMetadata(library, undefined, false, true);
        break;
      case(Action.Edit):
        this.editLibrary(library)
        break;
      case (Action.Delete):
        await this.deleteLibrary(library);
        break;
      default:
        break;
    }
  }

  performAction(action: ActionItem<Library>, library: Library) {
    if (typeof action.callback === 'function') {
      action.callback(action, library);
    }
  }


  async performBulkAction(action: ActionItem<Library>) {
    if (typeof action.callback === 'function') {
      await this.handleBulkAction(action, null);
    }
  }

  setupSelections() {
    this.selections = new SelectionModel<Library>(false, this.libraries);
    this.cdRef.markForCheck();
  }

  toggleAll() {
    this.selectAll = !this.selectAll;
    this.libraries.forEach(s => this.selections.toggle(s, this.selectAll));
    this.cdRef.markForCheck();
  }

  handleSelection(item: Library, index: number) {
    if (this.isShiftDown && this.lastSelectedIndex !== null) {
      // Bulk select items between the last selected item and the current one
      const start = Math.min(this.lastSelectedIndex, index);
      const end = Math.max(this.lastSelectedIndex, index);

      for (let i = start; i <= end; i++) {
        const library = this.libraries[i];
        if (!this.selections.isSelected(library)) {
          this.selections.toggle(library, true); // Select the item
        }
      }
    } else {
      // Toggle the clicked item
      this.selections.toggle(item);
    }

    // Update the last selected index
    this.lastSelectedIndex = index;

    // Manage the state of "Select All" and "Has Some Selected"
    const numberOfSelected = this.selections.selected().length;
    this.selectAll = numberOfSelected === this.libraries.length;
    //this.hasSomeSelected = numberOfSelected > 0 && numberOfSelected < this.libraries.length;

    this.cdRef.markForCheck();
  }

  // handleSelection(item: Library) {
  //   this.selections.toggle(item);
  //
  //   if (this.isShiftDown) {
  //     // Select multiple
  //
  //   }
  //
  //   const numberOfSelected = this.selections.selected().length;
  //   if (numberOfSelected == 0) {
  //     this.selectAll = false;
  //   } else if (numberOfSelected == this.selectedLibraries.length) {
  //     this.selectAll = true;
  //   }
  //   this.cdRef.markForCheck();
  // }

}
