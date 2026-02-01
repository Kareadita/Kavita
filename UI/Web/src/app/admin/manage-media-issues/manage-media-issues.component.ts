import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  EventEmitter,
  inject,
  OnInit,
  Output
} from '@angular/core';
import {filter, shareReplay} from 'rxjs';
import {KavitaMediaError} from '../_models/media-error';
import {ServerService} from 'src/app/_services/server.service';
import {EVENTS, MessageHubService} from 'src/app/_services/message-hub.service';
import {FormControl, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {FilterPipe} from '../../_pipes/filter.pipe';
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
  imports: [ReactiveFormsModule, FilterPipe, TranslocoDirective, UtcToLocalTimePipe, DefaultDatePipe, NgxDatatableModule, ResponsiveTableComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageMediaIssuesComponent implements OnInit {

  @Output() alertCount = new EventEmitter<number>();

  private readonly serverService = inject(ServerService);
  private readonly messageHub = inject(MessageHubService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly WikiLink = WikiLink;

  messageHubUpdate$ = this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef), filter(m => m.event === EVENTS.ScanSeries), shareReplay());

  data: Array<KavitaMediaError> = [];
  isLoading = true;
  formGroup = new FormGroup({
    filter: new FormControl('', [])
  });
  trackBy = (idx: number, item: KavitaMediaError) => `${item.filePath}`

  ngOnInit(): void {
    this.loadData();
    this.messageHubUpdate$.subscribe(_ => this.loadData());
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

}
