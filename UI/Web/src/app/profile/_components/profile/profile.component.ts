import {ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input} from '@angular/core';
import {Location, TitleCasePipe} from '@angular/common';
import {ReadingPaceComponent} from "../../../statistics/_components/reading-pace/reading-pace.component";
import {ActivityGraphComponent} from "../../../statistics/_components/activity-graph/activity-graph.component";
import {MemberInfo} from "../../../_models/user/member-info";
import {TranslocoDirective} from "@jsverse/transloco";
import {ImageService} from "../../../_services/image.service";
import {TimeAgoPipe} from "../../../_pipes/time-ago.pipe";
import {
  NgbNav,
  NgbNavChangeEvent,
  NgbNavContent,
  NgbNavItem,
  NgbNavLink,
  NgbNavOutlet
} from "@ng-bootstrap/ng-bootstrap";
import {tap} from "rxjs";
import {takeUntilDestroyed, toSignal} from "@angular/core/rxjs-interop";
import {ActivatedRoute} from "@angular/router";
import {PreferredFormatComponent} from "../../../statistics/_components/preferred-format/preferred-format.component";
import {StatisticsService} from "../../../_services/statistics.service";
import {StringBreakdownComponent} from "../../../statistics/_components/string-breakdown/string-breakdown.component";
import {UtcToLocaleDatePipe} from "../../../_pipes/utc-to-locale-date.pipe";
import {
  BucketSpreadChartComponent
} from "../../../statistics/_components/bucket-spread-chart/bucket-spread-chart.component";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {ProfileImageComponent} from "../profile-image/profile-image.component";
import {FavoriteAuthorsComponent} from "../../../statistics/_components/favourite-authors/favorite-authors.component";
import {
  LibraryAndTimeFilterGroup,
  LibraryAndTimeSelectorComponent
} from "../../../statistics/_components/library-and-time-selector/library-and-time-selector.component";
import {map} from "rxjs/operators";
import {StatsFilter} from "../../../statistics/_models/stats-filter";
import {LicenseService} from "../../../_services/license.service";
import {LoadingComponent} from "../../../shared/loading/loading.component";
import {ReadsByMonthComponent} from "../../../statistics/_components/reads-by-month/reads-by-month.component";
import {VirtualScrollerModule} from "@iharbeck/ngx-virtual-scroller";
import {NgxStarsModule} from "ngx-stars";
import {ProfileReviewListComponent} from "../profile-review-list/profile-review-list.component";
import {
  AvgTimeSpendReadingByHourComponent
} from "../../../statistics/_components/avg-time-spend-reading-by-hour/avg-time-spend-reading-by-hour.component";
import {ProfileStatBarComponent} from "../profile-stat-bar/profile-stat-bar.component";
import {ProfileOverviewComponent} from "../profile-overview/profile-overview.component";
import {CompactNumberPipe} from "../../../_pipes/compact-number.pipe";

enum TabID {
  Overview = 'overview-tab',
  Stats = 'stats-tab',
  Reviews = 'reviews-tab',
}

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [
    ReadingPaceComponent,
    ActivityGraphComponent,
    TranslocoDirective,
    TimeAgoPipe,
    NgbNav,
    NgbNavContent,
    NgbNavLink,
    NgbNavItem,
    NgbNavOutlet,
    PreferredFormatComponent,
    TitleCasePipe,
    StringBreakdownComponent,
    UtcToLocaleDatePipe,
    BucketSpreadChartComponent,
    ReactiveFormsModule,
    ProfileImageComponent,
    FavoriteAuthorsComponent,
    LibraryAndTimeSelectorComponent,
    LoadingComponent,
    ReadsByMonthComponent,
    VirtualScrollerModule,
    NgxStarsModule,
    ProfileReviewListComponent,
    AvgTimeSpendReadingByHourComponent,
    ProfileStatBarComponent,
    ProfileOverviewComponent,
    CompactNumberPipe,
  ],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProfileComponent {

  private readonly location = inject(Location);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly imageService = inject(ImageService);
  private readonly statsService = inject(StatisticsService);
  protected readonly licenseService = inject(LicenseService);


  // Set by angular from the resolver
  memberInfo = input.required<MemberInfo>();
  userId = computed(() => this.memberInfo().id);


  protected readonly genreBreakdown = this.statsService.getGenreBreakDownResource(() => this.filter(), () => this.userId());
  protected readonly tagsBreakdown = this.statsService.getTagBreakDownResource(() => this.filter(), () => this.userId());
  protected readonly wordSpreadResource = this.statsService.getWordSpread(() => this.filter(), () => this.userId());
  protected readonly pageSpreadResource = this.statsService.getPageSpread(() => this.filter(), () => this.userId());
  protected readonly readsByMonth = this.statsService.getReadsByMonths(() => this.filter(), () => this.userId());
  protected readonly avgTimeSpendReadingByHour = this.statsService.getAvgTimeSpendReadingByHour(() => this.filter(), () => this.userId());
  protected readonly totalReadsResource = this.statsService.getTotalReads(() => this.userId());

  activeTabId = TabID.Overview;


  filterForm = new FormGroup<LibraryAndTimeFilterGroup>({
    timeFilter: new FormGroup({
      startDate: new FormControl<Date | null>(null),
      endDate: new FormControl<Date | null>(null),
    }),
    libraries: new FormControl<number[]>([], { nonNullable: true }),
  });

  filter = toSignal(this.filterForm.valueChanges.pipe(
    map(value => value as StatsFilter),
  ));
  year = computed(() => this.filter()?.timeFilter.endDate?.getFullYear() ?? new Date().getFullYear());

  totalReads = computed(() => {
    if (!this.totalReadsResource.hasValue()) {
      return 0;
    }

    return this.totalReadsResource.value();
  });

  protected readonly backgroundImage = computed(() => {
    const m = this.memberInfo();
    if (!m) return '';
    try {
      return this.imageService.getUserCoverImage(this.userId());
    } catch {
      return '';
    }
  });

  constructor() {
    this.route.fragment.pipe(tap(frag => {
      const fragId = frag as TabID;
      if (frag !== null && this.activeTabId !== fragId) {
        this.updateUrl(fragId);
        this.activeTabId = fragId;
      }
    }), takeUntilDestroyed(this.destroyRef)).subscribe();
  }

  onNavChange(event: NgbNavChangeEvent) {
    this.updateUrl(event.nextId);
    this.activeTabId = event.nextId;
  }

  updateUrl(activeTab: TabID) {
    const tokens = this.location.path().split('#');
    const newUrl = `${tokens[0]}#${activeTab}`;
    this.location.replaceState(newUrl) // TODO: Look into making this a directive for tabs
  }




  protected readonly TabID = TabID;
  protected readonly window = window;

}
