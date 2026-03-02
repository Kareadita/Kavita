import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  HostListener,
  inject,
  OnInit,
  signal
} from '@angular/core';

import {ScrobbleProvider, ScrobblingService} from "../../_services/scrobbling.service";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {ScrobbleEvent, ScrobbleEventType} from "../../_models/scrobbling/scrobble-event";
import {ScrobbleEventTypePipe} from "../../_pipes/scrobble-event-type.pipe";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {ScrobbleEventSortField} from "../../_models/scrobbling/scrobble-event-filter";
import {debounceTime, take} from "rxjs/operators";
import {PaginatedResult} from "../../_models/pagination";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {translate, TranslocoModule} from "@jsverse/transloco";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {TranslocoLocaleModule} from "@jsverse/transloco-locale";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {LooseLeafOrDefaultNumber, SpecialVolumeNumber} from "../../_models/chapter";
import {ColumnMode, NgxDatatableModule} from "@siemens/ngx-datatable";
import {APP_BASE_HREF} from "@angular/common";
import {AccountService} from "../../_services/account.service";
import {ToastrService} from "ngx-toastr";
import {SelectionModel} from "../../typeahead/_models/selection-model";
import {ResponsiveTableComponent} from "../../shared/_components/responsive-table/responsive-table.component";
import {RouterLink} from "@angular/router";

export interface DataTablePage {
  pageNumber: number,
  size: number,
  totalElements: number,
  totalPages: number
}

@Component({
  selector: 'app-user-scrobble-history',
  imports: [ScrobbleEventTypePipe, ReactiveFormsModule, TranslocoModule,
    DefaultValuePipe, TranslocoLocaleModule, UtcToLocalTimePipe, NgbTooltip, NgxDatatableModule,
    ResponsiveTableComponent, RouterLink],
  templateUrl: './user-scrobble-history.component.html',
  styleUrls: ['./user-scrobble-history.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserScrobbleHistoryComponent implements OnInit {

  protected readonly SpecialVolumeNumber = SpecialVolumeNumber;
  protected readonly LooseLeafOrDefaultNumber = LooseLeafOrDefaultNumber;
  protected readonly ColumnMode = ColumnMode;
  protected readonly ScrobbleEventType = ScrobbleEventType;

  private readonly scrobblingService = inject(ScrobblingService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly toastr = inject(ToastrService);
  protected readonly accountService = inject(AccountService);
  protected readonly baseUrl = inject(APP_BASE_HREF);

  tokenExpired = signal(false);
  formGroup: FormGroup = new FormGroup({
    'filter': new FormControl('', [])
  });
  events = signal<ScrobbleEvent[]>([]);
  isLoading = signal(true);
  pageInfo = signal<DataTablePage>({
    pageNumber: 0,
    size: 10,
    totalElements: 0,
    totalPages: 0
  });
  private currentSort: {column: string; direction: string} = {
    column: 'lastModifiedUtc',
    direction: 'desc'
  };
  hasRunScrobbleGen = signal(false);

  selections = signal(new SelectionModel<ScrobbleEvent>(), { equal: () => false });
  selectAll = signal(false);
  isShiftDown = false;
  lastSelectedIndex: number | null = null;

  hasAnySelected = computed(() => this.selections().hasAnySelected());

  trackByEvents = (idx: number, data: ScrobbleEvent) => `${data.isProcessed}_${data.isErrored}_${data.id}`;

  @HostListener('document:keydown.shift', ['$event'])
  handleKeypress(_: Event) {
    this.isShiftDown = true;
  }

  @HostListener('document:keyup.shift', ['$event'])
  handleKeyUp(_: Event) {
    this.isShiftDown = false;
  }

  ngOnInit() {
    this.scrobblingService.hasRunScrobbleGen().subscribe(res => {
      this.hasRunScrobbleGen.set(res);
    });

    this.scrobblingService.hasTokenExpired(ScrobbleProvider.AniList).subscribe(hasExpired => {
      this.tokenExpired.set(hasExpired);
    });

    this.formGroup.get('filter')?.valueChanges.pipe(debounceTime(200), takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.loadPage();
    });

    this.loadPage(this.currentSort);
  }

  onPageChange(pageInfo: any) {
    this.pageInfo.update(p => ({...p, pageNumber: pageInfo.offset}));
    this.loadPage(this.currentSort);
  }

  updateSort(data: any) {
    this.currentSort = {
      column: data.column.prop,
      direction: data.newValue
    };
  }

  loadPage(sortEvent?: {column: string; direction: string}) {
    const page = (this.pageInfo().pageNumber || 0) + 1;
    const pageSize = this.pageInfo().size || 0;
    const isDescending = sortEvent?.direction === 'desc';
    const field = this.mapSortColumnField(sortEvent?.column);
    const query = this.formGroup.get('filter')?.value;

    this.isLoading.set(true);

    this.scrobblingService.getScrobbleEvents({query, field, isDescending}, page, pageSize)
      .pipe(take(1))
      .subscribe((result: PaginatedResult<ScrobbleEvent[]>) => {
      this.events.set(result.result);
      this.selections.set(new SelectionModel(false, result.result));

      this.pageInfo.set({
        pageNumber: this.pageInfo().pageNumber,
        totalPages: result.pagination.totalPages - 1,
        size: result.pagination.itemsPerPage,
        totalElements: result.pagination.totalItems
      });
      this.isLoading.set(false);
    });
  }

  private mapSortColumnField(column: string | undefined) {
    switch (column) {
      case 'createdUtc': return ScrobbleEventSortField.Created;
      case 'isProcessed': return ScrobbleEventSortField.IsProcessed;
      case 'lastModifiedUtc': return ScrobbleEventSortField.LastModified;
      case 'seriesName': return ScrobbleEventSortField.Series;
      case 'scrobbleEventType': return ScrobbleEventSortField.ScrobbleEvent;
    }
    return ScrobbleEventSortField.None;
  }

  generateScrobbleEvents() {
    this.scrobblingService.triggerScrobbleEventGeneration().subscribe(_ => {
      this.toastr.info(translate('toasts.scrobble-gen-init'))
    });
  }

  bulkDelete() {
    if (!this.selections().hasAnySelected()) {
      return;
    }

    const eventIds = this.selections().selected().map(e => e.id);

    this.scrobblingService.bulkRemoveEvents(eventIds).subscribe({
      next: () => {
        this.events.update(events => events.filter(e => !eventIds.includes(e.id)));
        this.selectAll.set(false);
        this.selections().clearSelected();
        this.pageInfo.update(p => ({...p, totalElements: p.totalElements - eventIds.length}));
      },
      error: err => {
        console.error(err);
      }
    });
  }

  toggleAll() {
    const newSelectAll = !this.selectAll();
    this.selectAll.set(newSelectAll);
    this.selections.update(s => {
      this.events().forEach(e => s.toggle(e, newSelectAll));
      return s;
    });
  }

  handleSelection(item: ScrobbleEvent, index: number) {
    this.selections.update(s => {
      if (this.isShiftDown && this.lastSelectedIndex !== null) {
        const start = Math.min(this.lastSelectedIndex, index);
        const end = Math.max(this.lastSelectedIndex, index);

        for (let i = start; i <= end; i++) {
          const event = this.events()[i];
          if (!s.isSelected(event, (e1, e2) => e1.id == e2.id)) {
            s.toggle(event, true);
          }
        }
      } else {
        s.toggle(item);
      }
      return s;
    });

    this.lastSelectedIndex = index;

    const numberOfSelected = this.selections().selected().length;
    this.selectAll.set(numberOfSelected === this.events().length);
  }
}
