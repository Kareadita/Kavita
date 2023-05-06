import { Component, EventEmitter, OnInit, QueryList, ViewChildren, inject } from '@angular/core';
import { BehaviorSubject, Observable, Subject, combineLatest, map, shareReplay, takeUntil } from 'rxjs';
import { SortEvent, SortableHeader, compare } from 'src/app/_single-module/table/_directives/sortable-header.directive';
import { KavitaMediaError } from '../_models/media-error';
import { ServerService } from 'src/app/_services/server.service';

@Component({
  selector: 'app-manage-alerts',
  templateUrl: './manage-alerts.component.html',
  styleUrls: ['./manage-alerts.component.scss']
})
export class ManageAlertsComponent implements OnInit {

  @ViewChildren(SortableHeader<KavitaMediaError>) headers!: QueryList<SortableHeader<KavitaMediaError>>;
  serverService = inject(ServerService); 
  private readonly onDestroy = new Subject<void>();

  rawData$: Observable<KavitaMediaError[]> = this.serverService.getMediaErrors().pipe(takeUntil(this.onDestroy), shareReplay());
  currentSort = new BehaviorSubject<SortEvent<KavitaMediaError>>({column: 'extension', direction: 'asc'});
  currentSort$: Observable<SortEvent<KavitaMediaError>> = this.currentSort.asObservable();
  dataUpdate$ = new EventEmitter<number>();
  data$: Observable<Array<KavitaMediaError>> = combineLatest([this.currentSort$, this.rawData$, this.dataUpdate$]).pipe(
    map(([sortConfig, data]) => {
      return {sortConfig, data};
    }),
    map(({ sortConfig, data }) => {
      return (sortConfig.column) ? data.sort((a: KavitaMediaError, b: KavitaMediaError) => {
        if (sortConfig.column === '') return 0;
        const res = compare(a[sortConfig.column], b[sortConfig.column]);
        return sortConfig.direction === 'asc' ? res : -res;
      }) : data;
    }),
    takeUntil(this.onDestroy)
  );
  
  

  constructor() {
    // this.data$ = combineLatest([this.currentSort$, this.rawData$, this.dataUpdate$]).pipe(
    //   map(([sortConfig, data]) => {
    //     return {sortConfig, fileBreakdown: data};
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

  ngOnInit(): void {
    this.data$.subscribe(d => console.log('data: ', d));
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

  clear() {
    this.serverService.clearMediaAlerts().subscribe(_ => this.dataUpdate$.next(1));
  }

//   protected fetchMore(event: IPageInfo) {
//     if (event.endIndex !== this.buffer.length-1) return;
//     this.loading = true;
//     this.fetchNextChunk(this.buffer.length, 10).then(chunk => {
//         this.buffer = this.buffer.concat(chunk);
//         this.loading = false;
//     }, () => this.loading = false);
// }

// protected fetchNextChunk(skip: number, limit: number): Promise<ListItem[]> {
//     return new Promise((resolve, reject) => {
//         ....
//     });
// }

}
