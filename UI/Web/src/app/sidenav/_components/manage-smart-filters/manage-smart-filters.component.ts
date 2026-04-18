import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  DestroyRef,
  inject,
  input,
  signal
} from '@angular/core';
import {FilterService} from "../../../_services/filter.service";
import {SmartFilter} from "../../../_models/metadata/v2/smart-filter";
import {TranslocoDirective} from "@jsverse/transloco";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {FilterPipe} from "../../../_pipes/filter.pipe";
import {APP_BASE_HREF, AsyncPipe} from "@angular/common";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {CarouselReelComponent} from "../../../carousel/_components/carousel-reel/carousel-reel.component";
import {SeriesCardComponent} from "../../../cards/series-card/series-card.component";
import {Observable, switchMap} from "rxjs";
import {SeriesService} from "../../../_services/series.service";
import {QueryContext} from "../../../_models/metadata/v2/query-context";
import {map, shareReplay} from "rxjs/operators";
import {FilterUtilitiesService} from "../../../shared/_services/filter-utilities.service";
import {ActionFactoryService} from "../../../_services/action-factory.service";
import {ActionResult} from "../../../_models/actionables/action-result";
import {CardActionablesComponent} from "src/app/_single-module/card-actionables/card-actionables.component";
import {FilterEntityType} from "../../../_models/metadata/v2/filter-entity-type";
import {ReadingListService} from "../../../_services/reading-list.service";
import {PersonService} from "../../../_services/person.service";
import {AnnotationService} from "../../../_services/annotation.service";

@Component({
  selector: 'app-manage-smart-filters',
  imports: [ReactiveFormsModule, TranslocoDirective, FilterPipe, CarouselReelComponent, SeriesCardComponent, AsyncPipe, CardActionablesComponent],
  templateUrl: './manage-smart-filters.component.html',
  styleUrls: ['./manage-smart-filters.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageSmartFiltersComponent {

  private readonly filterService = inject(FilterService);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly seriesService = inject(SeriesService);
  private readonly readingListService = inject(ReadingListService);
  private readonly personService = inject(PersonService);
  private readonly annotationService = inject(AnnotationService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly actionFactoryService = inject(ActionFactoryService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly baseUrl = inject(APP_BASE_HREF);

  target = input<'_self' | '_blank'>('_blank');

  filters = signal<SmartFilter[]>([]);
  hasFilterControl = computed(() => this.filters().length >= 5);

  listForm: FormGroup = new FormGroup({
    'filterQuery': new FormControl('', [])
  });
  filterApiMap: { [key: number]: Observable<any> } = {};
  actions = computed(() => this.actionFactoryService.getSmartFilterActions(this.filters()));

  filterList = (listItem: SmartFilter) => {
    const filterVal = (this.listForm.value.filterQuery || '').toLowerCase();
    return listItem.name.toLowerCase().indexOf(filterVal) >= 0;
  }

  constructor() {
    this.loadData();
  }

  getFilterLink(filter: SmartFilter) {
    switch (filter.entityType) {
      case FilterEntityType.Series:
        return this.baseUrl + 'all-series?' + filter.filter;
      case FilterEntityType.ReadingList:
        return this.baseUrl + 'lists?' + filter.filter;
      case FilterEntityType.Person:
        return this.baseUrl + 'browse/people?' + filter.filter;
      case FilterEntityType.Annotation:
        return this.baseUrl + 'browse/annotations?' + filter.filter;
    }
  }

  loadData() {
    this.filterService.getAllFilters().subscribe(filters => {
      this.filters.set([...filters]);

      this.filterApiMap = {};
      for(let filter of filters) {
        this.filterApiMap[filter.id] = this.filterUtilityService.decodeFilter(filter.filter).pipe(
          switchMap(filter => {
            switch (filter.entityType) {
              case FilterEntityType.Series:
                return this.seriesService.getAllSeriesV2(0, 20, filter, QueryContext.Dashboard);
              case FilterEntityType.ReadingList:
                return this.readingListService.getAllReadingLists(filter, 0, 20);
              case FilterEntityType.Person:
                return this.personService.getAuthorsToBrowse(filter, 0, 20);
              case FilterEntityType.Annotation:
                return this.annotationService.getAllAnnotationsFiltered(filter, 0, 20);
            }
          }))
          .pipe(map(d => d.result), takeUntilDestroyed(this.destroyRef), shareReplay({bufferSize: 1, refCount: true}));
      }

      this.cdRef.markForCheck();
    });
  }

  resetFilter() {
    this.listForm.get('filterQuery')?.setValue('');
    this.cdRef.markForCheck();
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
}
