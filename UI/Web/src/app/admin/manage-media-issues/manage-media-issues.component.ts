import {ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, output, signal} from '@angular/core';
import {filter, shareReplay} from 'rxjs';
import {KavitaMediaError} from '../_models/media-error';
import {ServerService} from 'src/app/_services/server.service';
import {EVENTS, MessageHubService} from 'src/app/_services/message-hub.service';
import {FormControl, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {takeUntilDestroyed, toSignal} from "@angular/core/rxjs-interop";
import {TranslocoDirective} from "@jsverse/transloco";
import {WikiLink} from "../../_models/wiki";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {DefaultDatePipe} from "../../_pipes/default-date.pipe";
import {NgxDatatableModule} from "@siemens/ngx-datatable";
import {ResponsiveTableComponent} from "../../shared/_components/responsive-table/responsive-table.component";

@Component({
  selector: 'app-manage-media-issues',
  templateUrl: './manage-media-issues.component.html',
  styleUrls: ['./manage-media-issues.component.scss'],
  imports: [ReactiveFormsModule, TranslocoDirective, UtcToLocalTimePipe, DefaultDatePipe, NgxDatatableModule, ResponsiveTableComponent],
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
  formGroup = new FormGroup({
    filter: new FormControl('', [])
  });
  private readonly filterQuery = toSignal(this.formGroup.controls.filter.valueChanges, {initialValue: ''});
  filteredData = computed(() => {
    const query = (this.filterQuery() || '').toLowerCase();
    return this.data().filter(item =>
      item.comment.toLowerCase().indexOf(query) >= 0 ||
      item.filePath.toLowerCase().indexOf(query) >= 0 ||
      item.details.indexOf(query) >= 0);
  });
  trackBy = (_: number, item: KavitaMediaError) => `${item.filePath}`

  ngOnInit(): void {
    this.loadData();
    this.messageHubUpdate$.subscribe(_ => this.loadData());
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
    this.serverService.clearMediaAlerts().subscribe(_ => this.loadData());
  }

}
