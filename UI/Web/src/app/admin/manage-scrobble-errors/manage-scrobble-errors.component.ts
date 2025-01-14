import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  EventEmitter,
  inject,
  OnInit,
  Output,
  QueryList,
  ViewChildren
} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {compare, SortableHeader, SortEvent} from "../../_single-module/table/_directives/sortable-header.directive";
import {KavitaMediaError} from "../_models/media-error";
import {EVENTS, MessageHubService} from "../../_services/message-hub.service";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {BehaviorSubject, filter, Observable, shareReplay} from "rxjs";
import {ScrobblingService} from "../../_services/scrobbling.service";
import {ScrobbleError} from "../../_models/scrobbling/scrobble-error";

import {SeriesService} from "../../_services/series.service";
import {EditSeriesModalComponent} from "../../cards/_modals/edit-series-modal/edit-series-modal.component";
import {NgbModal} from "@ng-bootstrap/ng-bootstrap";
import {FilterPipe} from "../../_pipes/filter.pipe";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {TranslocoModule} from "@jsverse/transloco";
import {DefaultDatePipe} from "../../_pipes/default-date.pipe";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {TranslocoLocaleModule} from "@jsverse/transloco-locale";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {DefaultModalOptions} from "../../_models/default-modal-options";
import {ColumnMode, NgxDatatableModule} from "@siemens/ngx-datatable";
import {DevicePlatformPipe} from "../../_pipes/device-platform.pipe";

@Component({
  selector: 'app-manage-scrobble-errors',
  standalone: true,
  imports: [ReactiveFormsModule, FilterPipe, LoadingComponent, SortableHeader, TranslocoModule, DefaultDatePipe, DefaultValuePipe, TranslocoLocaleModule, UtcToLocalTimePipe, DevicePlatformPipe, NgxDatatableModule],
  templateUrl: './manage-scrobble-errors.component.html',
  styleUrls: ['./manage-scrobble-errors.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageScrobbleErrorsComponent implements OnInit {
  @Output() scrobbleCount = new EventEmitter<number>();
  @ViewChildren(SortableHeader<KavitaMediaError>) headers!: QueryList<SortableHeader<KavitaMediaError>>;
  private readonly scrobbleService = inject(ScrobblingService);
  private readonly messageHub = inject(MessageHubService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly seriesService = inject(SeriesService);
  private readonly modalService = inject(NgbModal);

  messageHubUpdate$ = this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef), filter(m => m.event === EVENTS.ScanSeries), shareReplay());
  currentSort = new BehaviorSubject<SortEvent<ScrobbleError>>({column: 'created', direction: 'asc'});
  currentSort$: Observable<SortEvent<ScrobbleError>> = this.currentSort.asObservable();

  data: Array<ScrobbleError> = [];
  isLoading = true;
  formGroup = new FormGroup({
    filter: new FormControl('', [])
  });


  constructor() {}

  ngOnInit() {

    this.loadData();

    this.messageHubUpdate$.subscribe(_ => this.loadData());

    this.currentSort$.subscribe(sortConfig => {
      this.data = (sortConfig.column) ? this.data.sort((a: ScrobbleError, b: ScrobbleError) => {
        if (sortConfig.column === '') return 0;
        const res = compare(a[sortConfig.column], b[sortConfig.column]);
        return sortConfig.direction === 'asc' ? res : -res;
      }) : this.data;
      this.cdRef.markForCheck();
    });
  }

  onSort(evt: any) {
    //SortEvent<KavitaMediaError>
    this.currentSort.next(evt);

    // Must clear out headers here
    this.headers.forEach((header) => {
      if (header.sortable !== evt.column) {
        header.direction = '';
      }
    });
  }

  loadData() {
    this.isLoading = true;
    this.cdRef.markForCheck();
    this.scrobbleService.getScrobbleErrors().subscribe(d => {
      this.data = d;
      this.isLoading = false;
      this.scrobbleCount.emit(d.length);
      this.cdRef.detectChanges();
    });
  }

  clear() {
    this.scrobbleService.clearScrobbleErrors().subscribe(_ => this.loadData());
  }

  filterList = (listItem: ScrobbleError) => {
    const query = (this.formGroup.get('filter')?.value || '').toLowerCase();
    return listItem.comment.toLowerCase().indexOf(query) >= 0 || listItem.details.toLowerCase().indexOf(query) >= 0;
  }

  editSeries(seriesId: number) {
    this.seriesService.getSeries(seriesId).subscribe(series => {
      const modalRef = this.modalService.open(EditSeriesModalComponent, DefaultModalOptions);
      modalRef.componentInstance.series = series;
    });
  }

  protected readonly filter = filter;
  protected readonly ColumnMode = ColumnMode;
}
