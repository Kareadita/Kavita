import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, OnInit} from '@angular/core';
import {CommonModule} from '@angular/common';

import {ScrobblingService} from "../../_services/scrobbling.service";
import {shareReplay} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {ScrobbleEvent, ScrobbleEventType} from "../../_models/scrobbling/scrobble-event";
import {ScrobbleEventTypePipe} from "../scrobble-event-type.pipe";
import {NgbPagination} from "@ng-bootstrap/ng-bootstrap";
import {ScrobbleEventSortField} from "../../_models/scrobbling/scrobble-event-filter";
import {debounceTime, map, take, tap} from "rxjs/operators";
import {PaginatedResult, Pagination} from "../../_models/pagination";
import {SortableHeader, SortEvent} from "../table/_directives/sortable-header.directive";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";

@Component({
  selector: 'app-user-scrobble-history',
  standalone: true,
  imports: [CommonModule, ScrobbleEventTypePipe, NgbPagination, ReactiveFormsModule, SortableHeader],
  templateUrl: './user-scrobble-history.component.html',
  styleUrls: ['./user-scrobble-history.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserScrobbleHistoryComponent implements OnInit {

  private readonly scrobbleService = inject(ScrobblingService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  pagination: Pagination | undefined;
  events: Array<ScrobbleEvent> = [];
  formGroup: FormGroup = new FormGroup({
    'filter': new FormControl('', [])
  });

  get ScrobbleEventType() { return ScrobbleEventType; }

  ngOnInit() {
    this.loadPage({column: 'created', direction: 'desc'});

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

    this.scrobbleService.getScrobbleEvents({query, field, isDescending}, page, pageSize)
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
