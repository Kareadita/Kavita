import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  DestroyRef,
  EventEmitter,
  inject,
  input,
  OnInit,
  signal
} from '@angular/core';
import {ActivatedRoute, Router} from '@angular/router';
import {BulkSelectionService} from '../cards/bulk-selection.service';
import {UtilityService} from '../shared/_services/utility.service';
import {SeriesAddedEvent} from '../_models/events/series-added-event';
import {Library} from '../_models/library/library';
import {Pagination} from '../_models/pagination';
import {Series} from '../_models/series';
import {FilterEvent, SortField} from '../_models/metadata/series-filter';
import {ActionFactoryService} from '../_services/action-factory.service';
import {LibraryService} from '../_services/library.service';
import {EVENTS, MessageHubService} from '../_services/message-hub.service';
import {SeriesService} from '../_services/series.service';
import {NavService} from '../_services/nav.service';
import {FilterUtilitiesService} from '../shared/_services/filter-utilities.service';
import {JumpKey} from '../_models/jumpbar/jump-key';
import {SeriesRemovedEvent} from '../_models/events/series-removed-event';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {BulkOperationsComponent} from '../cards/bulk-operations/bulk-operations.component';
import {SeriesCardComponent} from '../cards/series-card/series-card.component';
import {CardDetailLayoutComponent} from '../cards/card-detail-layout/card-detail-layout.component';
import {DecimalPipe} from '@angular/common';
import {
  SideNavCompanionBarComponent
} from '../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';
import {TranslocoDirective} from "@jsverse/transloco";
import {FilterV2} from "../_models/metadata/v2/filter-v2";
import {FilterComparison} from "../_models/metadata/v2/filter-comparison";
import {FilterField} from "../_models/metadata/v2/filter-field";
import {CardActionablesComponent} from "../_single-module/card-actionables/card-actionables.component";
import {LoadingComponent} from "../shared/loading/loading.component";
import {debounceTime, ReplaySubject, tap} from "rxjs";
import {SeriesFilterSettings} from "../metadata-filter/filter-settings";
import {MetadataService} from "../_services/metadata.service";
import {ActionResult} from "../_models/actionables/action-result";
import {KavitaTitleStrategy} from "../_services/kavita-title.strategy";
import {getWritableResolvedData} from "../../libs/route-util";
import {JumpbarService} from "../_services/jumpbar.service";

@Component({
    selector: 'app-library-detail',
    templateUrl: './library-detail.component.html',
    styleUrls: ['./library-detail.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [SideNavCompanionBarComponent, CardActionablesComponent,
        CardDetailLayoutComponent, SeriesCardComponent, BulkOperationsComponent, DecimalPipe, TranslocoDirective, LoadingComponent]
})
export class LibraryDetailComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly seriesService = inject(SeriesService);
  private readonly libraryService = inject(LibraryService);
  private readonly kavitaTitleStrategy = inject(KavitaTitleStrategy);
  private readonly actionFactoryService = inject(ActionFactoryService);
  private readonly hubService = inject(MessageHubService);
  private readonly utilityService = inject(UtilityService);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  public readonly navService = inject(NavService);
  public readonly bulkSelectionService = inject(BulkSelectionService);
  public readonly metadataService = inject(MetadataService);
  private readonly jumpbarService = inject(JumpbarService);

  // From Resolver
  readonly library = getWritableResolvedData(this.route, 'library');
  libraryId = input.required<number>();
  libraryName = computed(() => this.library().name);

  series = signal<Series[]>([]);
  loadingSeries = false;
  pagination: Pagination = {currentPage: 0, totalPages: 0, totalItems: 0, itemsPerPage: 0};
  actions = computed(() => this.actionFactoryService.getLibraryActions());
  filter: FilterV2<FilterField> | undefined = undefined;
  filterSettings: SeriesFilterSettings = new SeriesFilterSettings();
  filterOpen: EventEmitter<boolean> = new EventEmitter();
  filterActive: boolean = false;
  filterActiveCheck!: FilterV2<FilterField>;
  refresh: EventEmitter<void> = new EventEmitter();
  jumpKeys = signal<JumpKey[]>([]);
  bulkLoader: boolean = false;

  tabs: Array<{title: string, fragment: string, icon: string}> = [
    {title: 'library-tab', fragment: '', icon: 'fa-landmark'},
    {title: 'recommended-tab', fragment: 'recommended', icon: 'fa-award'},
  ];
  active = this.tabs[0];

  loadPageSource = new ReplaySubject(1);
  loadPage$ = this.loadPageSource.asObservable();


  ngOnInit(): void {

    this.router.routeReuseStrategy.shouldReuseRoute = () => false;

    this.bulkSelectionService.registerDataSource('series', () => this.series());
    this.bulkSelectionService.registerPostAction((res: ActionResult<Series>) => {
      if (res.effect === 'none') return;
      this.loadPage();
    });

    this.route.data.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(data => {
      this.filter = data['filter'] as FilterV2<FilterField, SortField>;

      const defaultStmt = {field: FilterField.Libraries, value: this.libraryId() + '', comparison: FilterComparison.Equal};

      if (this.filter == null) {
        this.filter = this.metadataService.createDefaultFilterDto('series');
        this.filter.statements.push(defaultStmt);
      }


      this.filterActiveCheck = this.metadataService.createDefaultFilterDto('series');
      this.filterActiveCheck!.statements.push(defaultStmt);
      this.filterSettings.presetsV2 =  this.filter;

      this.loadPage$.pipe(takeUntilDestroyed(this.destroyRef), debounceTime(100), tap(_ => this.loadPage())).subscribe();

      this.cdRef.markForCheck();
    });

    this.hubService.messages$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      if (event.event === EVENTS.SeriesAdded) {
        const seriesAdded = event.payload as SeriesAddedEvent;
        if (seriesAdded.libraryId !== this.libraryId()) return;
        if (!this.utilityService.deepEqual(this.filter, this.filterActiveCheck)) {
          this.loadPageSource.next(true);
          return;
        }
        this.seriesService.getSeries(seriesAdded.seriesId).subscribe(s => {
          if (this.series().filter(sObj => s.id === sObj.id).length > 0) return;
          this.series.set([...this.series(), s].sort((s1: Series, s2: Series) => {
            if (s1.sortName < s2.sortName) return -1;
            if (s1.sortName > s2.sortName) return 1;
            return 0;
          }));
          this.pagination.totalItems++;
          this.cdRef.markForCheck();
          this.refresh.emit();
        });


      } else if (event.event === EVENTS.SeriesRemoved) {
        const seriesRemoved = event.payload as SeriesRemovedEvent;
        if (seriesRemoved.libraryId !== this.libraryId()) return;
        if (!this.utilityService.deepEqual(this.filter, this.filterActiveCheck)) {
          this.loadPageSource.next(true);
          return;
        }

        this.series.set(this.series().filter(s => s.id !== seriesRemoved.seriesId));
        this.pagination.totalItems--;
        this.cdRef.markForCheck();
        this.refresh.emit();
      }
    });
  }

  updateFilter(data: FilterEvent<FilterField, SortField>) {
    if (data.filterV2 === undefined) return;
    this.filter = data.filterV2;

    if (data.isFirst) {
      this.loadPageSource.next(true);
      return;
    }

    this.filterUtilityService.updateUrlFromFilter(this.filter).subscribe((encodedFilter) => {
      this.loadPageSource.next(true);
    });
  }

  loadPage() {
    this.loadingSeries = true;
    this.filterActive = !this.utilityService.deepEqual(this.filter, this.filterActiveCheck);
    this.cdRef.markForCheck();

    this.seriesService.getSeriesForLibraryV2(undefined, undefined, this.filter)
      .subscribe(series => {
        this.series.set([...series.result]);
        this.pagination = series.pagination;
        this.loadingSeries = false;
        this.jumpKeys.set(this.jumpbarService.getJumpKeys(series.result, s => s.sortName));
        this.cdRef.markForCheck();
      });
  }

  trackByIdentity = (index: number, item: Series) => `${item.id}_${item.name}_${item.localizedName}_${item.pagesRead}`;

  protected handleActionCallback(event: ActionResult<Library>) {
    switch (event.effect) {
      case 'update':
        this.library.set({...event.entity});
        this.kavitaTitleStrategy.setFormattedTitle(event.entity.name);
        break;
      case 'remove':
      case 'reload':
        this.router.navigateByUrl(this.router.url); // TODO: This is a hack until we have an api for non-admin users
        break;
      case 'none':
        break;
    }
  }
}
