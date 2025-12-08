import {ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input} from '@angular/core';
import {Location, TitleCasePipe} from '@angular/common';
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
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {ActivatedRoute} from "@angular/router";
import {StatisticsService} from "../../../_services/statistics.service";
import {UtcToLocalDatePipe} from "../../../_pipes/utc-to-locale-date.pipe";
import {ReactiveFormsModule} from "@angular/forms";
import {ProfileImageComponent} from "../profile-image/profile-image.component";
import {LicenseService} from "../../../_services/license.service";
import {LoadingComponent} from "../../../shared/loading/loading.component";
import {VirtualScrollerModule} from "@iharbeck/ngx-virtual-scroller";
import {NgxStarsModule} from "ngx-stars";
import {ProfileReviewListComponent} from "../profile-review-list/profile-review-list.component";
import {ProfileOverviewComponent} from "../profile-overview/profile-overview.component";
import {CompactNumberPipe} from "../../../_pipes/compact-number.pipe";
import {ProfileStatsComponent} from "../profile-stats/profile-stats.component";

enum TabID {
  Overview = 'overview-tab',
  Stats = 'stats-tab',
  Reviews = 'reviews-tab',
}

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [
    TranslocoDirective,
    TimeAgoPipe,
    NgbNav,
    NgbNavContent,
    NgbNavLink,
    NgbNavItem,
    NgbNavOutlet,
    TitleCasePipe,
    UtcToLocalDatePipe,
    ReactiveFormsModule,
    ProfileImageComponent,
    LoadingComponent,
    VirtualScrollerModule,
    NgxStarsModule,
    ProfileReviewListComponent,
    ProfileOverviewComponent,
    CompactNumberPipe,
    ProfileStatsComponent,
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


  protected readonly totalReadsResource = this.statsService.getTotalReads(() => this.userId());

  activeTabId = TabID.Overview;

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
