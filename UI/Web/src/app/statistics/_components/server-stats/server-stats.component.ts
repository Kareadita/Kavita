import {ChangeDetectionStrategy, Component, computed, DestroyRef, HostListener, inject, signal} from '@angular/core';
import {Router} from '@angular/router';
import {NgbModal, NgbNav, NgbNavContent, NgbNavItem, NgbNavLink, NgbNavOutlet} from '@ng-bootstrap/ng-bootstrap';
import {Observable, ReplaySubject, shareReplay} from 'rxjs';
import {FilterUtilitiesService} from 'src/app/shared/_services/filter-utilities.service';
import {Breakpoint, UtilityService} from 'src/app/shared/_services/utility.service';
import {Series} from 'src/app/_models/series';
import {ImageService} from 'src/app/_services/image.service';
import {MetadataService} from 'src/app/_services/metadata.service';
import {StatisticsService} from 'src/app/_services/statistics.service';
import {PieDataItem} from '../../_models/pie-data-item';
import {ServerStatistics} from '../../_models/server-statistics';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {BytesPipe} from '../../../_pipes/bytes.pipe';
import {TimeDurationPipe} from '../../../_pipes/time-duration.pipe';
import {CompactNumberPipe} from '../../../_pipes/compact-number.pipe';
import {DayBreakdownComponent} from '../day-breakdown/day-breakdown.component';
import {ReadingActivityComponent} from '../reading-activity/reading-activity.component';
import {PublicationStatusStatsComponent} from '../publication-status-stats/publication-status-stats.component';
import {FileBreakdownStatsComponent} from '../file-breakdown-stats/file-breakdown-stats.component';
import {StatListComponent, StatListItem} from '../stat-list/stat-list.component';
import {IconAndTitleComponent} from '../../../shared/icon-and-title/icon-and-title.component';
import {AsyncPipe, DecimalPipe, Location} from '@angular/common';
import {TranslocoDirective} from "@jsverse/transloco";
import {AccountService} from "../../../_services/account.service";
import {ReactiveFormsModule} from "@angular/forms";
import {LibraryAndTimeSelectorComponent} from "../library-and-time-selector/library-and-time-selector.component";
import {StatsFilter} from "../../_models/stats-filter";
import {MostActiveUsersComponent} from "../most-active-users/most-active-users.component";

enum TabID {
  Stats = 'stats-tab',
  Management = 'management-tab',
}

@Component({
    selector: 'app-server-stats',
    templateUrl: './server-stats.component.html',
    styleUrls: ['./server-stats.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [IconAndTitleComponent, StatListComponent, FileBreakdownStatsComponent, PublicationStatusStatsComponent,
    ReadingActivityComponent, DayBreakdownComponent, AsyncPipe, DecimalPipe, CompactNumberPipe, TimeDurationPipe,
    BytesPipe, TranslocoDirective, ReactiveFormsModule, LibraryAndTimeSelectorComponent, MostActiveUsersComponent,
    NgbNav, NgbNavContent, NgbNavLink, NgbNavItem, NgbNavOutlet]
})
export class ServerStatsComponent {
  private readonly statService = inject(StatisticsService);
  private readonly router = inject(Router);
  private readonly imageService = inject(ImageService);
  private readonly metadataService = inject(MetadataService);
  private readonly modalService = inject(NgbModal);
  private readonly utilityService = inject(UtilityService);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly location = inject(Location);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly accountService = inject(AccountService);

  protected readonly TabID = TabID;

  activeTabId = TabID.Stats;

  userId = computed(() => this.accountService.currentUserSignal()?.id);
  readonly filter = signal<StatsFilter | undefined>(undefined);
  readonly year = signal<number>(new Date().getFullYear());

  readonly releaseYearsResource = this.statService.getTopYearsResource();
  readonly releaseYears = computed(() => {
    return (this.releaseYearsResource.value() ?? []).map(r => {
      return {name: r.value + '', value: r.count};
    }) as StatListItem[];
  });

  readonly statsResource = this.statService.getServerStatisticsResource();
  readonly popularLibraries = computed(() => {
    return (this.statsResource.value()?.mostActiveLibraries ?? []).map(r => {
      return {name: r.value.name, value: r.count};
    }) as StatListItem[];
  });

  readonly popularSeries = computed(() => {
    return (this.statsResource.value()?.mostReadSeries ?? []).map(r => {
      return {name: r.value.name, value: r.count, data: r.value};
    }) as StatListItem[];
  });
  readonly mostPopularSeriesCover = computed(() => {
    const popular = this.popularSeries();
    if (!popular || popular.length === 0) {
      return '';
    }
    return this.imageService.getSeriesCoverImage((popular[0].data as Series).id)
  })

  stats$!: Observable<ServerStatistics>;
  openSeries = (data: PieDataItem) => {
    const series = data.extra as Series;
    this.router.navigate(['library', series.libraryId, 'series', series.id]);
  }

  breakpointSubject = new ReplaySubject<Breakpoint>(1);


  @HostListener('window:resize', [])
  @HostListener('window:orientationchange', [])
  onResize() {
    this.breakpointSubject.next(this.utilityService.getActiveBreakpoint());
  }


  constructor() {
    this.breakpointSubject.next(this.utilityService.getActiveBreakpoint());

    this.stats$ = this.statService.getServerStatistics().pipe(takeUntilDestroyed(this.destroyRef), shareReplay());
  }
}
