import { Component, QueryList, ViewChildren } from '@angular/core';
import { BehaviorSubject, Observable, Subject, combineLatest, map, shareReplay, takeUntil } from 'rxjs';
import { SortEvent, SortableHeader, compare } from 'src/app/_single-module/table/_directives/sortable-header.directive';
import { FileExtension } from 'src/app/statistics/_models/file-breakdown';
import { KavitaMediaError } from '../_models/media-error';

@Component({
  selector: 'app-manage-alerts',
  templateUrl: './manage-alerts.component.html',
  styleUrls: ['./manage-alerts.component.scss']
})
export class ManageAlertsComponent {

  @ViewChildren(SortableHeader<KavitaMediaError>) headers!: QueryList<SortableHeader<KavitaMediaError>>;

  rawData$!: Observable<KavitaMediaError>;
  files$!: Observable<Array<KavitaMediaError>>;
  currentSort = new BehaviorSubject<SortEvent<KavitaMediaError>>({column: 'created', direction: 'asc'});
  currentSort$: Observable<SortEvent<KavitaMediaError>> = this.currentSort.asObservable();
  private readonly onDestroy = new Subject<void>();

  constructor() {
    // this.rawData$ = this.statService.getFileBreakdown().pipe(takeUntil(this.onDestroy), shareReplay());

    // this.files$ = combineLatest([this.currentSort$, this.rawData$]).pipe(
    //   map(([sortConfig, data]) => {
    //     return {sortConfig, fileBreakdown: data.fileBreakdown};
    //   }),
    //   map(({ sortConfig, fileBreakdown }) => {
    //     return (sortConfig.column) ? fileBreakdown.sort((a: KavitaMediaError, b: KavitaMediaError) => {
    //       if (sortConfig.column === '') return 0;
    //       const res = compare(a[sortConfig.column], b[sortConfig.column]);
    //       return sortConfig.direction === 'asc' ? res : -res;
    //     }) : fileBreakdown;
    //   }),
    //   takeUntil(this.onDestroy)
    // );
  }

  onSort(evt: SortEvent<KavitaMediaError>) {
    this.currentSort.next(evt);

    // Must clear out headers here
    this.headers.forEach((header) => {
      if (header.sortable !== evt.column) {
        header.direction = '';
      }
    });
  }

}
