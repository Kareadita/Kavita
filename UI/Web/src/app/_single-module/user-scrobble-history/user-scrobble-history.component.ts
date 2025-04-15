import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, OnInit} from '@angular/core';

import {ScrobbleProvider, ScrobblingService} from "../../_services/scrobbling.service";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {ScrobbleEvent, ScrobbleEventType} from "../../_models/scrobbling/scrobble-event";
import {ScrobbleEventTypePipe} from "../../_pipes/scrobble-event-type.pipe";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {ScrobbleEventSortField} from "../../_models/scrobbling/scrobble-event-filter";
import {debounceTime, take} from "rxjs/operators";
import {PaginatedResult} from "../../_models/pagination";
import {SortEvent} from "../table/_directives/sortable-header.directive";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {translate, TranslocoModule} from "@jsverse/transloco";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {TranslocoLocaleModule} from "@jsverse/transloco-locale";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {LooseLeafOrDefaultNumber, SpecialVolumeNumber} from "../../_models/chapter";
import {ColumnMode, NgxDatatableModule} from "@siemens/ngx-datatable";
import {AsyncPipe} from "@angular/common";
import {AccountService} from "../../_services/account.service";
import {ToastrService} from "ngx-toastr";

export interface DataTablePage {
  pageNumber: number,
  size: number,
  totalElements: number,
  totalPages: number
}

@Component({
    selector: 'app-user-scrobble-history',
  imports: [ScrobbleEventTypePipe, ReactiveFormsModule, TranslocoModule,
    DefaultValuePipe, TranslocoLocaleModule, UtcToLocalTimePipe, NgbTooltip, NgxDatatableModule, AsyncPipe],
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
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly toastr = inject(ToastrService);
  protected readonly accountService = inject(AccountService);



  tokenExpired = false;
  formGroup: FormGroup = new FormGroup({
    'filter': new FormControl('', [])
  });
  events: Array<ScrobbleEvent> = [];
  isLoading: boolean = true;
  pageInfo: DataTablePage = {
    pageNumber: 0,
    size: 10,
    totalElements: 0,
    totalPages: 0
  }
  private currentSort: SortEvent<ScrobbleEvent> = {
    column: 'lastModifiedUtc',
    direction: 'desc'
  };
  hasRunScrobbleGen: boolean = false;

  ngOnInit() {

    this.pageInfo.pageNumber = 0;
    this.cdRef.markForCheck();

    this.scrobblingService.hasRunScrobbleGen().subscribe(res => {
      this.hasRunScrobbleGen = res;
      this.cdRef.markForCheck();
    })

    this.scrobblingService.hasTokenExpired(ScrobbleProvider.AniList).subscribe(hasExpired => {
      this.tokenExpired = hasExpired;
      this.cdRef.markForCheck();
    });

    this.formGroup.get('filter')?.valueChanges.pipe(debounceTime(200), takeUntilDestroyed(this.destroyRef)).subscribe(query => {
      this.loadPage();
    });

    this.loadPage(this.currentSort);
  }

  onPageChange(pageInfo: any) {
    this.pageInfo.pageNumber = pageInfo.offset;
    this.cdRef.markForCheck();

    this.loadPage(this.currentSort);
  }

  updateSort(data: any) {
    this.currentSort = {
      column: data.column.prop,
      direction: data.newValue
    };
  }

  loadPage(sortEvent?: SortEvent<ScrobbleEvent>) {
    const page = (this.pageInfo?.pageNumber || 0) + 1;
    const pageSize = this.pageInfo?.size || 0;
    const isDescending = sortEvent?.direction === 'desc';
    const field = this.mapSortColumnField(sortEvent?.column);
    const query = this.formGroup.get('filter')?.value;

    this.isLoading = true;
    this.cdRef.markForCheck();

    this.scrobblingService.getScrobbleEvents({query, field, isDescending}, page, pageSize)
      .pipe(take(1))
      .subscribe((result: PaginatedResult<ScrobbleEvent[]>) => {
      this.events = result.result;

      this.pageInfo.totalPages = result.pagination.totalPages - 1; // ngx-datatable is 0 based, Kavita is 1 based
      this.pageInfo.size = result.pagination.itemsPerPage;
      this.pageInfo.totalElements = result.pagination.totalItems;
      this.isLoading = false;
      this.cdRef.markForCheck();
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
}
