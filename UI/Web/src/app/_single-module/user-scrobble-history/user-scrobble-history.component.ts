import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, OnInit} from '@angular/core';
import {CommonModule} from '@angular/common';
import {TableModule} from "../table/table.module";
import {ScrobblingService} from "../../_services/scrobbling.service";
import {shareReplay} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {ScrobbleEvent, ScrobbleEventType} from "../../_models/scrobbling/scrobble-event";
import {ScrobbleEventTypePipe} from "../scrobble-event-type.pipe";
import {NgbPagination} from "@ng-bootstrap/ng-bootstrap";
import {ScrobbleEventSortField} from "../../_models/scrobbling/scrobble-event-filter";
import {debounceTime, map, take, tap} from "rxjs/operators";
import {PaginatedResult, Pagination} from "../../_models/pagination";
import {SortEvent} from "../table/_directives/sortable-header.directive";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";

@Component({
  selector: 'app-user-scrobble-history',
  standalone: true,
  imports: [CommonModule, TableModule, ScrobbleEventTypePipe, NgbPagination, ReactiveFormsModule],
  templateUrl: './user-scrobble-history.component.html',
  styleUrls: ['./user-scrobble-history.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserScrobbleHistoryComponent implements OnInit {

  private readonly scrobbleService = inject(ScrobblingService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  page: number = 0;
  pageSize: number = 30;
  totalPages: number = 1;
  pagination: Pagination | undefined;
  events: Array<ScrobbleEvent> = [];
  formGroup: FormGroup = new FormGroup({
    'filter': new FormControl('', [])
  });

  get ScrobbleEventType() { return ScrobbleEventType; }

  ngOnInit() {
    this.loadPage();

    this.formGroup.get('filter')?.valueChanges.pipe(debounceTime(200), takeUntilDestroyed(this.destroyRef)).subscribe(query => {
      this.loadPage();
    })
  }

  onPageChange(pageNum: number) {
    if (this.pagination) {
      this.pagination.currentPage = pageNum;
      this.loadPage();
    }
  }

  loadPage(sortEvent?: SortEvent<ScrobbleEvent>) {
    if (sortEvent && this.pagination) {
      this.pagination.currentPage = 1;
      this.cdRef.markForCheck();
    }
    const page = this.pagination?.currentPage || 0;
    const pageSize = this.pagination?.itemsPerPage || 0;

    this.scrobbleService.getScrobbleEvents({query: this.formGroup.get('filter')?.value, field: this.mapSortColumnField(sortEvent?.column), isDescending: sortEvent?.direction === 'desc'}, page, pageSize)
      .pipe(take(1))
      .subscribe((result: PaginatedResult<ScrobbleEvent[]>) => {
      this.events = result.result;
      this.pagination = result.pagination;
      this.cdRef.markForCheck();
    });
  }

  private mapSortColumnField(column: string | undefined) {
    switch (column) {
      case 'created': return ScrobbleEventSortField.Created;
      case 'isProcessed': return ScrobbleEventSortField.IsProcessed;
      case 'lastModified': return ScrobbleEventSortField.LastModified;
      case 'seriesName': return ScrobbleEventSortField.Series;
    }
    return ScrobbleEventSortField.None;
  }


}
