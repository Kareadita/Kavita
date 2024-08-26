import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, OnInit} from '@angular/core';

import {ScrobbleProvider, ScrobblingService} from "../../_services/scrobbling.service";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {ScrobbleEvent, ScrobbleEventType} from "../../_models/scrobbling/scrobble-event";
import {ScrobbleEventTypePipe} from "../../_pipes/scrobble-event-type.pipe";
import {NgbPagination, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {ScrobbleEventSortField} from "../../_models/scrobbling/scrobble-event-filter";
import {debounceTime, take} from "rxjs/operators";
import {PaginatedResult, Pagination} from "../../_models/pagination";
import {SortableHeader, SortEvent} from "../table/_directives/sortable-header.directive";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {translate, TranslocoModule} from "@jsverse/transloco";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {TranslocoLocaleModule} from "@jsverse/transloco-locale";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {ToastrService} from "ngx-toastr";
import {LooseLeafOrDefaultNumber, SpecialVolumeNumber} from "../../_models/chapter";

@Component({
  selector: 'app-user-scrobble-history',
  standalone: true,
  imports: [ScrobbleEventTypePipe, NgbPagination, ReactiveFormsModule, SortableHeader, TranslocoModule,
    DefaultValuePipe, TranslocoLocaleModule, UtcToLocalTimePipe, NgbTooltip],
  templateUrl: './user-scrobble-history.component.html',
  styleUrls: ['./user-scrobble-history.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserScrobbleHistoryComponent implements OnInit {

  private readonly scrobblingService = inject(ScrobblingService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly toastr = inject(ToastrService);
  protected readonly ScrobbleEventType = ScrobbleEventType;

  pagination: Pagination | undefined;
  events: Array<ScrobbleEvent> = [];
  formGroup: FormGroup = new FormGroup({
    'filter': new FormControl('', [])
  });

  ngOnInit() {
    this.loadPage({column: 'createdUtc', direction: 'desc'});

    this.scrobblingService.hasTokenExpired(ScrobbleProvider.AniList).subscribe(hasExpired => {
      if (hasExpired) {
        this.toastr.error(translate('toasts.anilist-token-expired'));
      }
      this.cdRef.markForCheck();
    });

    this.formGroup.get('filter')?.valueChanges.pipe(debounceTime(200), takeUntilDestroyed(this.destroyRef)).subscribe(query => {
      this.loadPage();
    })
  }

  onPageChange(pageNum: number) {
    let prevPage = 0;
    if (this.pagination) {
      prevPage = this.pagination.currentPage;
      this.pagination.currentPage = pageNum;
    }
    if (prevPage !== pageNum) {
      this.loadPage();
    }

  }

  updateSort(sortEvent: SortEvent<ScrobbleEvent>) {
    this.loadPage(sortEvent);
  }

  loadPage(sortEvent?: SortEvent<ScrobbleEvent>) {
    if (sortEvent && this.pagination) {
      this.pagination.currentPage = 1;
      this.cdRef.markForCheck();
    }
    const page = this.pagination?.currentPage || 0;
    const pageSize = this.pagination?.itemsPerPage || 0;
    const isDescending = sortEvent?.direction === 'desc';
    const field = this.mapSortColumnField(sortEvent?.column);
    const query = this.formGroup.get('filter')?.value;

    this.scrobblingService.getScrobbleEvents({query, field, isDescending}, page, pageSize)
      .pipe(take(1))
      .subscribe((result: PaginatedResult<ScrobbleEvent[]>) => {
      this.events = result.result;
      this.pagination = result.pagination;
      this.cdRef.markForCheck();
    });
  }

  private mapSortColumnField(column: string | undefined) {
    switch (column) {
      case 'createdUtc': return ScrobbleEventSortField.Created;
      case 'isProcessed': return ScrobbleEventSortField.IsProcessed;
      case 'lastModifiedUtc': return ScrobbleEventSortField.LastModified;
      case 'seriesName': return ScrobbleEventSortField.Series;
    }
    return ScrobbleEventSortField.None;
  }


    protected readonly SpecialVolumeNumber = SpecialVolumeNumber;
  protected readonly LooseLeafOrDefaultNumber = LooseLeafOrDefaultNumber;
}
