import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  HostListener,
  inject,
  OnInit
} from '@angular/core';
import {NgbModal, NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {ToastrService} from 'ngx-toastr';
import {distinctUntilChanged, filter, take} from 'rxjs/operators';
import {ConfirmService} from 'src/app/shared/confirm.service';
import {
  LibrarySettingsModalComponent
} from 'src/app/sidenav/_modals/library-settings-modal/library-settings-modal.component';
import {NotificationProgressEvent} from 'src/app/_models/events/notification-progress-event';
import {ScanSeriesEvent} from 'src/app/_models/events/scan-series-event';
import {Library} from 'src/app/_models/library/library';
import {LibraryService} from 'src/app/_services/library.service';
import {EVENTS, Message, MessageHubService} from 'src/app/_services/message-hub.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {SentenceCasePipe} from '../../_pipes/sentence-case.pipe';
import {TimeAgoPipe} from '../../_pipes/time-ago.pipe';
import {LibraryTypePipe} from '../../_pipes/library-type.pipe';
import {RouterLink} from '@angular/router';
import {translate, TranslocoModule} from "@jsverse/transloco";
import {DefaultDatePipe} from "../../_pipes/default-date.pipe";
import {AsyncPipe, NgTemplateOutlet} from "@angular/common";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {Breakpoint, UtilityService} from "../../shared/_services/utility.service";
import {Action, ActionFactoryService, ActionItem} from "../../_services/action-factory.service";
import {ActionService} from "../../_services/action.service";
import {CardActionablesComponent} from "../../_single-module/card-actionables/card-actionables.component";
import {BehaviorSubject, catchError, Observable} from "rxjs";
import {Select2Module} from "ng-select2-component";
import {SelectionModel} from "../../typeahead/_models/selection-model";
import {
  CopySettingsFromLibraryModalComponent
} from "../_modals/copy-settings-from-library-modal/copy-settings-from-library-modal.component";
import {FormControl, FormGroup} from "@angular/forms";

@Component({
    selector: 'app-manage-library',
    templateUrl: './manage-library.component.html',
    styleUrls: ['./manage-library.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [RouterLink, NgbTooltip, LibraryTypePipe, TimeAgoPipe, SentenceCasePipe, TranslocoModule, DefaultDatePipe,
    AsyncPipe, LoadingComponent, CardActionablesComponent, Select2Module, NgTemplateOutlet]
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
  protected readonly Action = Action;

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
  selections!: SelectionModel<Library>;
  selectAll: boolean = false;
  bulkMode = false;
  bulkAction: Action | null = null;
  sourceCopyToLibrary: Library | null = null;
  bulkForm = new FormGroup({'includeType': new FormControl(false)});
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
      this.resetBulkMode();
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


  async applyBulkAction() {
    // Get Selected libraries
    let selected = this.selections.selected();

    // Remove the source library id from selected (if applicable)
    if (this.bulkAction === Action.CopySettings) {
      selected = selected.filter(l => l.id !== this.sourceCopyToLibrary!.id);
    }

    if (selected.length === 0) {
      await this.confirmService.alert(translate('toasts.must-select-library'));
      return;
    }

    switch(this.bulkAction) {
      case (Action.Scan):
        await this.confirmService.alert(translate('toasts.bulk-scan'));
        this.bulkMode = true;
        this.cdRef.markForCheck();
        this.libraryService.scanMultipleLibraries(selected.map(l => l.id)).subscribe(_ => this.resetBulkMode());
        break;
      case Action.RefreshMetadata:
        if (!await this.confirmService.confirm(translate('toasts.bulk-covers'))) return;
        this.bulkMode = true;
        this.cdRef.markForCheck();
        this.libraryService.refreshMetadataMultipleLibraries(selected.map(l => l.id), true, false).subscribe(() => {
          this.getLibraries();
          this.resetBulkMode();
        });
        break
      case Action.AnalyzeFiles:
        this.bulkMode = true;
        this.cdRef.markForCheck();
        this.libraryService.analyzeFilesMultipleLibraries(selected.map(l => l.id)).subscribe(() => {
          this.getLibraries();
          this.resetBulkMode();
        });
        break;
      case Action.GenerateColorScape:
        this.bulkMode = true;
        this.cdRef.markForCheck();
        this.libraryService.refreshMetadataMultipleLibraries(selected.map(l => l.id), false, true).subscribe(() => {
          this.getLibraries();
          this.resetBulkMode();
        });
        break;
      case Action.Delete:
        this.bulkMode = true;
        this.cdRef.markForCheck();
        const libIds = selected.map(l => l.id);
        if (!await this.confirmService.confirm(translate('toasts.bulk-delete-libraries', {count: libIds.length}))) return;
        this.libraryService.deleteMultiple(libIds)
          .pipe(catchError((_, obs) => {
            this.resetBulkMode();
            return obs;
          }))
          .subscribe(() => {
          this.getLibraries();
          this.resetBulkMode();
        })
        break;
      case Action.CopySettings:
        // Remove the source library from the list
        if (selected.length === 1 && selected[0].id === this.sourceCopyToLibrary!.id) {
          return;
        }

        this.bulkMode = true;
        this.cdRef.markForCheck();

        const includeType = this.bulkForm.get('includeType')!.value + '' == 'true';
        this.libraryService.copySettingsFromLibrary(this.sourceCopyToLibrary!.id, selected.map(l => l.id), includeType).subscribe(() => {
          this.getLibraries();
          this.resetBulkMode();
        });
        break;
    }
  }

  async handleBulkAction(action: ActionItem<Library>, library : Library | null) {
    this.bulkAction = action.action;
    this.cdRef.markForCheck();

    switch (action.action) {
      case(Action.Scan):
      case(Action.RefreshMetadata):
      case(Action.GenerateColorScape):
      case (Action.Delete):
      case (Action.AnalyzeFiles):
        await this.applyBulkAction();
        break;
      case (Action.CopySettings):

        // Prompt the user for the library then wait for them to manually trigger applyBulkAction
        const ref = this.modalService.open(CopySettingsFromLibraryModalComponent, {size: 'lg', fullscreen: 'md'});
        ref.componentInstance.libraries = this.libraries;
        ref.closed.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((res: number | null) => {
          if (res === null) return;
          // res will be the library the user chose
          this.bulkMode = true;
          this.sourceCopyToLibrary = this.libraries.filter(l => l.id === res)[0];
          this.cdRef.markForCheck();
        });
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

    this.cdRef.markForCheck();
  }


  resetBulkMode() {
    this.bulkAction = null;
    this.bulkMode = false;
    this.sourceCopyToLibrary = null;
    this.libraries.forEach(s => {
      if (this.selections.isSelected(s)) {
        this.selections.toggle(s, false)
      }
    });
    this.selectAll = false;
      this.cdRef.markForCheck();
  }
}
