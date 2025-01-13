import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  EventEmitter,
  OnInit,
  Output,
  QueryList,
  ViewChildren,
  inject,
  DestroyRef
} from '@angular/core';
import { BehaviorSubject, Observable, filter, shareReplay } from 'rxjs';
import { SortEvent, SortableHeader, compare } from 'src/app/_single-module/table/_directives/sortable-header.directive';
import { KavitaMediaError } from '../_models/media-error';
import { ServerService } from 'src/app/_services/server.service';
import { EVENTS, MessageHubService } from 'src/app/_services/message-hub.service';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { FilterPipe } from '../../_pipes/filter.pipe';
import { LoadingComponent } from '../../shared/loading/loading.component';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {WikiLink} from "../../_models/wiki";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {DefaultDatePipe} from "../../_pipes/default-date.pipe";
import {ColumnMode, NgxDatatableModule} from "@siemens/ngx-datatable";

@Component({
    selector: 'app-manage-media-issues',
    templateUrl: './manage-media-issues.component.html',
    styleUrls: ['./manage-media-issues.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
    imports: [ReactiveFormsModule, LoadingComponent, FilterPipe, SortableHeader, TranslocoDirective, UtcToLocalTimePipe, DefaultDatePipe, NgxDatatableModule]
})
export class ManageMediaIssuesComponent implements OnInit {

  @Output() alertCount = new EventEmitter<number>();
  @ViewChildren(SortableHeader<KavitaMediaError>) headers!: QueryList<SortableHeader<KavitaMediaError>>;

  private readonly serverService = inject(ServerService);
  private readonly messageHub = inject(MessageHubService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly WikiLink = WikiLink;

  messageHubUpdate$ = this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef), filter(m => m.event === EVENTS.ScanSeries), shareReplay());
  currentSort = new BehaviorSubject<SortEvent<KavitaMediaError>>({column: 'extension', direction: 'asc'});
  currentSort$: Observable<SortEvent<KavitaMediaError>> = this.currentSort.asObservable();

  data: Array<KavitaMediaError> = [];
  isLoading = true;
  formGroup = new FormGroup({
    filter: new FormControl('', [])
  });

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

  filterList = (listItem: KavitaMediaError) => {
    const query = (this.formGroup.get('filter')?.value || '').toLowerCase();
    return listItem.comment.toLowerCase().indexOf(query) >= 0 || listItem.filePath.toLowerCase().indexOf(query) >= 0 || listItem.details.indexOf(query) >= 0;
  }
    protected readonly ColumnMode = ColumnMode;
  protected readonly translate = translate;
}
