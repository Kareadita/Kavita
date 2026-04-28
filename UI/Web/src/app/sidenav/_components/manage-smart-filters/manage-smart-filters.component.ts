import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  input, OnInit,
  signal,
  TemplateRef,
  viewChild
} from '@angular/core';
import {FilterService} from "../../../_services/filter.service";
import {SmartFilter} from "../../../_models/metadata/v2/smart-filter";
import {TranslocoDirective} from "@jsverse/transloco";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {APP_BASE_HREF, AsyncPipe} from "@angular/common";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {CarouselReelComponent} from "../../../carousel/_components/carousel-reel/carousel-reel.component";
import {SeriesCardComponent} from "../../../cards/series-card/series-card.component";
import {filter, Observable, switchMap, tap} from "rxjs";
import {SeriesService} from "../../../_services/series.service";
import {QueryContext} from "../../../_models/metadata/v2/query-context";
import {map, shareReplay} from "rxjs/operators";
import {FilterUtilitiesService} from "../../../shared/_services/filter-utilities.service";
import {ActionFactoryService} from "../../../_services/action-factory.service";
import {ActionResult} from "../../../_models/actionables/action-result";
import {CardActionablesComponent} from "src/app/_single-module/card-actionables/card-actionables.component";
import {allFilterEntityTypes, FilterEntityType} from "../../../_models/metadata/v2/filter-entity-type";
import {ReadingListService} from "../../../_services/reading-list.service";
import {PersonService} from "../../../_services/person.service";
import {AnnotationService} from "../../../_services/annotation.service";
import {FilterEntityTypePipe} from "../../../_pipes/filter-entity-type.pipe";
import {CardConfigFactory} from "../../../_services/card-config-factory.service";
import {EntityCardComponent} from "../../../cards/entity-card/entity-card.component";
import {PromotedIconComponent} from "../../../shared/_components/promoted-icon/promoted-icon.component";
import {CardEntity, CardEntityFactory} from "../../../_models/card/card-entity";
import {Action} from "../../../_models/actionables/action";
import {User} from "../../../_models/user/user";
import {ActionItem} from "../../../_models/actionables/action-item";
import {EVENTS, MessageHubService} from "../../../_services/message-hub.service";
import {DashboardService} from "../../../_services/dashboard.service";
import {NavService} from "../../../_services/nav.service";

@Component({
  selector: 'app-manage-smart-filters',
  imports: [ReactiveFormsModule, TranslocoDirective, CarouselReelComponent, SeriesCardComponent, AsyncPipe, CardActionablesComponent, FilterEntityTypePipe, EntityCardComponent, PromotedIconComponent],
  templateUrl: './manage-smart-filters.component.html',
  styleUrls: ['./manage-smart-filters.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageSmartFiltersComponent implements OnInit {

  private readonly filterService = inject(FilterService);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly seriesService = inject(SeriesService);
  private readonly readingListService = inject(ReadingListService);
  private readonly personService = inject(PersonService);
  private readonly annotationService = inject(AnnotationService);
  private readonly actionFactoryService = inject(ActionFactoryService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly cardConfigFactory = inject(CardConfigFactory);
  private readonly messageHub = inject(MessageHubService);
  private readonly dashboardService = inject(DashboardService);
  private readonly sideNavService = inject(NavService);
  protected readonly baseUrl = inject(APP_BASE_HREF);

  target = input<'_self' | '_blank'>('_blank');

  protected readonly filters = signal<SmartFilter[]>([]);
  protected readonly hasFilterControl = computed(() => this.filters().length >= 1);
  protected readonly filteredItems = computed(() => {
    const items = this.filters();
    const filterVal = this.filterQuery().toString().toLowerCase();
    const entityType = this.filterEntityType();

    if (!filterVal) {
      return items.filter(item => item.entityType === entityType);
    }

    return items.filter(item => item.entityType === entityType)
      .filter(item => item.name.toLowerCase().includes(filterVal));
  });

  listForm: FormGroup = new FormGroup({
    'filterQuery': new FormControl('', []),
    'entityType': new FormControl<FilterEntityType>(FilterEntityType.Series, []),
  });
  protected readonly filterApiMap = signal<{ [key: number]: Observable<any> }>({});
  protected readonly actions = computed(() => this.actionFactoryService.getSmartFilterActions(this.filters(), this.shouldRenderFunc.bind(this)));
  protected readonly filterQuery = signal<string>('');
  protected readonly filterEntityType = signal<FilterEntityType>(FilterEntityType.Series);
  private readonly dashboardFilters = signal<Set<number>>(new Set<number>());
  private readonly sideNavFilters = signal<Set<number>>(new Set<number>());

  protected titleTemplateRef = viewChild<TemplateRef<{ $implicit: CardEntity }>>('title');
  protected readonly readingListConfig = computed(() => this.cardConfigFactory.forReadingList({titleRef: this.titleTemplateRef(), overrides: {allowSelection: false, actionableFunc: () => []}}));

  constructor() {
    this.loadData();

    this.listForm.get('filterQuery')?.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef),
      tap(val => this.filterQuery.set(val))
    ).subscribe();
    this.listForm.get('entityType')?.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef),
      tap(val => this.filterEntityType.set(parseInt(val + '', 10)))
    ).subscribe();

    this.messageHub.messages$.pipe(
      tap(msg => {
        if (msg.event === EVENTS.SideNavUpdate) {
          this.sideNavService.getSideNavStreams(true).subscribe(streams => {
            this.sideNavFilters.set(new Set(streams.map(stream => stream.id)));
          });
        } else if (msg.event === EVENTS.DashboardUpdate) {
          this.dashboardService.getDashboardStreams(true).subscribe(streams => {
            this.dashboardFilters.set(new Set(streams.map(stream => stream.id)));
          });
        }
      }),
    ).subscribe();
  }

  ngOnInit() {
    this.sideNavService.getSideNavStreams(true).subscribe(streams => {
      this.sideNavFilters.set(new Set(streams.map(stream => stream.smartFilterId)));
    });
    this.dashboardService.getDashboardStreams(true).subscribe(streams => {
      this.dashboardFilters.set(new Set(streams.map(stream => stream.smartFilterId)));
    });
  }

  getFilterLink(filter: SmartFilter) {
    return this.baseUrl + FilterUtilitiesService.getFilterLink(filter.entityType, filter.filter);
  }

  shouldRenderFunc(action: ActionItem<SmartFilter>, item: SmartFilter, user: User) {
    switch (action.action) {
      case Action.AddToDashboard:
        return !this.dashboardFilters().has(item.id);
      case Action.AddToSideNav:
        return !this.sideNavFilters().has(item.id);
    }

    return true;
  }

  loadData() {
    this.filterService.getAllFilters().subscribe(filters => {
      this.filters.set([...filters]);

      const newApiMap: { [key: number]: Observable<any> } = {};
      this.filterApiMap.set({});
      for(let filter of filters) {
        newApiMap[filter.id] = this.filterUtilityService.decodeFilter(filter.filter).pipe(
          switchMap(filter => {
            switch (filter.entityType) {
              case FilterEntityType.Series:
                return this.seriesService.getAllSeriesV2(0, 20, filter, QueryContext.Dashboard).pipe(map(d => d.result));
              case FilterEntityType.ReadingList:
                return this.readingListService.getAllReadingLists(filter, 0, 20).pipe(map(d => d.result.map(rl => CardEntityFactory.readingList(rl))));
              case FilterEntityType.Person:
                return this.personService.getAuthorsToBrowse(filter, 0, 20).pipe(map(d => d.result));
              case FilterEntityType.Annotation:
                return this.annotationService.getAllAnnotationsFiltered(filter, 0, 20).pipe(map(d => d.result));
            }
          }))
          .pipe(takeUntilDestroyed(this.destroyRef), shareReplay({bufferSize: 1, refCount: true}));
      }

      this.filterApiMap.set(newApiMap);
    });
  }

  resetFilter() {
    this.listForm.get('filterQuery')?.setValue('');
  }

  isErrored(filter: SmartFilter) {
    return !decodeURIComponent(filter.filter).includes('¦');
  }

  handleActionCallback(result: ActionResult<SmartFilter>) {
    switch (result.effect) {
      case 'update':
      case 'remove':
      case 'reload':
        this.resetFilter();
        this.loadData();
        break
      case 'none':
        break;
    }
  }

  protected readonly allFilterEntityTypes = allFilterEntityTypes;
  protected readonly FilterEntityType = FilterEntityType;
}
