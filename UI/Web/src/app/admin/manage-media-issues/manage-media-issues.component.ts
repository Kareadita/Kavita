import {ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, output, signal} from '@angular/core';
import {filter, shareReplay} from 'rxjs';
import {KavitaMediaError} from '../_models/media-error';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {TranslocoDirective} from "@jsverse/transloco";
import {WikiLink} from "../../_models/wiki";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {DefaultDatePipe} from "../../_pipes/default-date.pipe";
import {NgxDatatableModule} from "@siemens/ngx-datatable";
import {ResponsiveTableComponent} from "../../shared/_components/responsive-table/responsive-table.component";
import {ServerService} from "../../_services/server.service";
import {EVENTS, MessageHubService} from "../../_services/message-hub.service";
import {FilterFieldComponent} from "../../shared/_components/filter-field/filter-field.component";
import {filteredBy} from "../../_helpers/filtered";

@Component({
  selector: 'app-manage-media-issues',
  templateUrl: './manage-media-issues.component.html',
  styleUrls: ['./manage-media-issues.component.scss'],
  imports: [TranslocoDirective, UtcToLocalTimePipe, DefaultDatePipe, NgxDatatableModule, ResponsiveTableComponent, FilterFieldComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageMediaIssuesComponent implements OnInit {

  readonly alertCount = output<number>();

  private readonly serverService = inject(ServerService);
  private readonly messageHub = inject(MessageHubService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly WikiLink = WikiLink;

  messageHubUpdate$ = this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef),
    filter(m => m.event === EVENTS.ScanSeries), shareReplay());

  data = signal<KavitaMediaError[]>([]);
  isLoading = signal(true);
  filterQuery = signal('');

  filteredData = filteredBy(this.data, this.filterQuery, 'comment', 'filePath', 'details');
  trackBy = (_: number, item: KavitaMediaError) => `${item.filePath}`

  ngOnInit(): void {
    this.loadData();
    this.messageHubUpdate$.subscribe(() => this.loadData());
  }


  loadData() {
    this.isLoading.set(true);
    this.serverService.getMediaErrors().subscribe(d => {
      this.data.set([...d]);
      this.isLoading.set(false);
      this.alertCount.emit(d.length);
    });
  }

  clear() {
    this.serverService.clearMediaAlerts().subscribe(() => this.loadData());
  }

}
