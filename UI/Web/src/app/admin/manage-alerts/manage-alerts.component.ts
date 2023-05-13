import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, OnInit, Output, QueryList, ViewChildren, inject } from '@angular/core';
import { BehaviorSubject, Observable, Subject, combineLatest, filter, map, shareReplay, takeUntil } from 'rxjs';
import { SortEvent, SortableHeader, compare } from 'src/app/_single-module/table/_directives/sortable-header.directive';
import { KavitaMediaError } from '../_models/media-error';
import { ServerService } from 'src/app/_services/server.service';
import { EVENTS, MessageHubService } from 'src/app/_services/message-hub.service';

@Component({
  selector: 'app-manage-alerts',
  templateUrl: './manage-alerts.component.html',
  styleUrls: ['./manage-alerts.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageAlertsComponent implements OnInit {

  @Output() alertCount = new EventEmitter<number>();
  @ViewChildren(SortableHeader<KavitaMediaError>) headers!: QueryList<SortableHeader<KavitaMediaError>>;
  private readonly serverService = inject(ServerService); 
  private readonly messageHub = inject(MessageHubService); 
  private readonly cdRef = inject(ChangeDetectorRef); 
  private readonly onDestroy = new Subject<void>();

  messageHubUpdate$ = this.messageHub.messages$.pipe(takeUntil(this.onDestroy), filter(m => m.event === EVENTS.ScanSeries), shareReplay());
  currentSort = new BehaviorSubject<SortEvent<KavitaMediaError>>({column: 'extension', direction: 'asc'});
  currentSort$: Observable<SortEvent<KavitaMediaError>> = this.currentSort.asObservable();

  data: Array<KavitaMediaError> = [];
  isLoading = true;
  

  constructor() {}

  ngOnInit(): void {

    this.loadData();

    this.messageHubUpdate$.subscribe(_ => this.loadData());

    this.currentSort$.subscribe(sortConfig => {
      this.data = (sortConfig.column) ? this.data.sort((a: KavitaMediaError, b: KavitaMediaError) => {
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
    this.serverService.getMediaErrors().subscribe(d => {
      this.data = d;
      this.isLoading = false;
      this.alertCount.emit(d.length);
      this.cdRef.detectChanges();
    });
  }

  clear() {
    this.serverService.clearMediaAlerts().subscribe(_ => this.loadData());
  }
}
