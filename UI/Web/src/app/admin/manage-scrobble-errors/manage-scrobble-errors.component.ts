import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  EventEmitter,
  inject,
  OnInit,
  Output,
  signal
} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {EVENTS, MessageHubService} from "../../_services/message-hub.service";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {filter, shareReplay} from "rxjs";
import {ScrobblingService} from "../../_services/scrobbling.service";
import {ScrobbleError} from "../../_models/scrobbling/scrobble-error";

import {SeriesService} from "../../_services/series.service";
import {FilterPipe} from "../../_pipes/filter.pipe";
import {TranslocoModule} from "@jsverse/transloco";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {TranslocoLocaleModule} from "@jsverse/transloco-locale";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {ColumnMode, NgxDatatableModule} from "@siemens/ngx-datatable";
import {ActionService} from "../../_services/action.service";
import {ResponsiveTableComponent} from "../../shared/_components/responsive-table/responsive-table.component";

@Component({
    selector: 'app-manage-scrobble-errors',
  imports: [ReactiveFormsModule, FilterPipe, TranslocoModule, DefaultValuePipe, TranslocoLocaleModule, UtcToLocalTimePipe, NgxDatatableModule, ResponsiveTableComponent],
    templateUrl: './manage-scrobble-errors.component.html',
    styleUrls: ['./manage-scrobble-errors.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageScrobbleErrorsComponent implements OnInit {
  protected readonly filter = filter;
  protected readonly ColumnMode = ColumnMode;

  private readonly scrobbleService = inject(ScrobblingService);
  private readonly messageHub = inject(MessageHubService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly seriesService = inject(SeriesService);
  private readonly actionService = inject(ActionService);

  @Output() scrobbleCount = new EventEmitter<number>();


  messageHubUpdate$ = this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef),
    filter(m => m.event === EVENTS.ScanSeries), shareReplay());

  data: Array<ScrobbleError> = [];
  isLoading = signal<boolean>(true);
  formGroup = new FormGroup({
    filter: new FormControl('', [])
  });
  trackBy = (index: number, item: ScrobbleError) => `${item.seriesId}`;


  ngOnInit() {
    this.loadData();
    this.messageHubUpdate$.subscribe(_ => this.loadData());
  }

  loadData() {
    this.isLoading.set(true);
    this.cdRef.markForCheck();
    this.scrobbleService.getScrobbleErrors().subscribe(d => {
      this.data = d;
      this.isLoading.set(false);
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

  fixMatch(seriesId: number) {
    this.seriesService.getSeries(seriesId).subscribe(series => {
      this.actionService.matchSeries(series, (result) => {
        if (!result) return;
        this.data = [...this.data.filter(s => s.seriesId !== series.id)];
        this.cdRef.markForCheck();
      });
    });
  }
}
