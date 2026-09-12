import {ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, output, signal} from '@angular/core';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {filter, shareReplay} from "rxjs";
import {TranslocoModule} from "@jsverse/transloco";
import {TranslocoLocaleModule} from "@jsverse/transloco-locale";
import {NgxDatatableModule} from "@siemens/ngx-datatable";
import {ScrobblingService} from "../../../_services/scrobbling.service";
import {DefaultValuePipe} from "../../../_pipes/default-value.pipe";
import {UtcToLocalTimePipe} from "../../../_pipes/utc-to-local-time.pipe";
import {ResponsiveTableComponent} from "../../../shared/_components/responsive-table/responsive-table.component";
import {EVENTS, MessageHubService} from "../../../_services/message-hub.service";
import {SeriesService} from "../../../_services/series.service";
import {ActionService} from "../../../_services/action.service";
import {ScrobbleError} from "../../../_models/scrobbling/scrobble-error";
import {FilterFieldComponent} from "../../../shared/_components/filter-field/filter-field.component";
import {filteredBy} from "../../../_helpers/filtered";

@Component({
  selector: 'app-manage-scrobble-errors',
  imports: [TranslocoModule, DefaultValuePipe, TranslocoLocaleModule, UtcToLocalTimePipe, NgxDatatableModule, ResponsiveTableComponent, FilterFieldComponent],
  templateUrl: './manage-scrobble-errors.component.html',
  styleUrls: ['./manage-scrobble-errors.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageScrobbleErrorsComponent implements OnInit {
  protected readonly filter = filter;

  private readonly scrobbleService = inject(ScrobblingService);
  private readonly messageHub = inject(MessageHubService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly seriesService = inject(SeriesService);
  private readonly actionService = inject(ActionService);

  readonly scrobbleCount = output<number>();

  messageHubUpdate$ = this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef),
    filter(m => m.event === EVENTS.ScanSeries), shareReplay());

  isLoading = signal<boolean>(true);
  data = signal<ScrobbleError[]>([]);
  filterQuery = signal('');

  filteredData = filteredBy(this.data, this.filterQuery, 'comment', 'details');

  trackBy = (index: number, item: ScrobbleError) => `${index}_${item.seriesId}`;


  ngOnInit() {
    this.loadData();
    this.messageHubUpdate$.subscribe(_ => this.loadData());
  }

  loadData() {
    this.isLoading.set(true);
    this.scrobbleService.getScrobbleErrors().subscribe(d => {
      this.data.set(d);
      this.isLoading.set(false);
      this.scrobbleCount.emit(d.length);
    });
  }

  clear() {
    this.scrobbleService.clearScrobbleErrors().subscribe(_ => this.loadData());
  }

  fixMatch(seriesId: number) {
    this.seriesService.getSeries(seriesId).subscribe(series => {
      this.actionService.matchSeries(series, (result) => {
        if (!result) return;
        this.data.update(x => [...x.filter(s => s.seriesId !== series.id)]);
      });
    });
  }
}
