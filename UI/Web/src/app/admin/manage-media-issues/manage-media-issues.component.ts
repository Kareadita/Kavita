import {ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, output, signal} from '@angular/core';
import {filter, shareReplay} from 'rxjs';
import {allMediaErrorReasons, KavitaMediaError, MediaErrorReason} from '../_models/media-error';
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
import {SettingSelectComponent} from "../../settings/_components/setting-enum-select/setting-select.component";
import {form, FormField} from "@angular/forms/signals";
import {MediaErrorReasonPipe} from "../../_pipes/media-error-reason.pipe";

interface FormModel {
  reason: MediaErrorReason | null;
  filterText: string;
}


@Component({
  selector: 'app-manage-media-issues',
  templateUrl: './manage-media-issues.component.html',
  styleUrls: ['./manage-media-issues.component.scss'],
  imports: [TranslocoDirective, UtcToLocalTimePipe, DefaultDatePipe, NgxDatatableModule, ResponsiveTableComponent, FilterFieldComponent, SettingSelectComponent, FormField, MediaErrorReasonPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageMediaIssuesComponent implements OnInit {

  readonly alertCount = output<number>();

  private readonly serverService = inject(ServerService);
  private readonly messageHub = inject(MessageHubService);
  private readonly destroyRef = inject(DestroyRef);


  messageHubUpdate$ = this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef),
    filter(m => m.event === EVENTS.ScanSeries), shareReplay());

  data = signal<KavitaMediaError[]>([]);
  isLoading = signal(true);
  filterQuery = signal('');
  private readonly formModel = signal<FormModel>({
    filterText: '',
    reason: null
  });
  formGroup = form(this.formModel);

  filteredData = filteredBy(this.data, this.filterQuery, 'comment', 'filePath', 'details');

  filteredData2 = computed(() => {
    const filter = this.formModel();
    const data = this.data();

    return data.filter(d => filter.reason != null && filter.reason === d.reason);
  });

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
  //
  // clear() {
  //   this.serverService.clearMediaAlerts().subscribe(() => this.loadData());
  // }

  protected readonly WikiLink = WikiLink;
  protected readonly allMediaErrorReasons = allMediaErrorReasons;

}
