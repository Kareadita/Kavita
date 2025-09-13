import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, Input} from '@angular/core';
import {FilterService} from "../../../_services/filter.service";
import {SmartFilter} from "../../../_models/metadata/v2/smart-filter";
import {TranslocoDirective} from "@jsverse/transloco";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {FilterPipe} from "../../../_pipes/filter.pipe";
import {ActionService} from "../../../_services/action.service";
import {NgbModal} from "@ng-bootstrap/ng-bootstrap";
import {APP_BASE_HREF, AsyncPipe} from "@angular/common";
import {EditSmartFilterModalComponent} from "../edit-smart-filter-modal/edit-smart-filter-modal.component";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {CarouselReelComponent} from "../../../carousel/_components/carousel-reel/carousel-reel.component";
import {SeriesCardComponent} from "../../../cards/series-card/series-card.component";
import {Observable, switchMap} from "rxjs";
import {SeriesService} from "../../../_services/series.service";
import {QueryContext} from "../../../_models/metadata/v2/query-context";
import {map, shareReplay} from "rxjs/operators";
import {FilterUtilitiesService} from "../../../shared/_services/filter-utilities.service";
import {Action, ActionFactoryService, ActionItem} from "../../../_services/action-factory.service";

@Component({
  selector: 'app-manage-smart-filters',
  imports: [ReactiveFormsModule, TranslocoDirective, FilterPipe, CarouselReelComponent, SeriesCardComponent, AsyncPipe],
  templateUrl: './manage-smart-filters.component.html',
  styleUrls: ['./manage-smart-filters.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageSmartFiltersComponent {

  private readonly filterService = inject(FilterService);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly seriesService = inject(SeriesService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly actionService = inject(ActionService);
  private readonly actionFactoryService = inject(ActionFactoryService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly modelService = inject(NgbModal);
  protected readonly baseUrl = inject(APP_BASE_HREF);

  @Input() target: '_self' | '_blank' = '_blank';

  filters: Array<SmartFilter> = [];
  listForm: FormGroup = new FormGroup({
    'filterQuery': new FormControl('', [])
  });
  filterApiMap: { [key: string]: Observable<any> } = {};
  actions: Array<ActionItem<SmartFilter>> = this.actionFactoryService.getSmartFilterActions(this.handleAction.bind(this));

  filterList = (listItem: SmartFilter) => {
    const filterVal = (this.listForm.value.filterQuery || '').toLowerCase();
    return listItem.name.toLowerCase().indexOf(filterVal) >= 0;
  }

  constructor() {
    this.loadData();
  }

  loadData() {
    this.filterService.getAllFilters().subscribe(filters => {
      this.filters = filters;

      this.filterApiMap = {};
      for(let filter of filters) {
        this.filterApiMap[filter.name] = this.filterUtilityService.decodeFilter(filter.filter).pipe(
          switchMap(filter => {
            return this.seriesService.getAllSeriesV2(0, 20, filter, QueryContext.Dashboard);
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

  handleAction(action: ActionItem<SmartFilter>, smartFilter: SmartFilter) {
    switch (action.action) {
      case Action.Edit:
        this.editFilter(smartFilter);
        break;
      case Action.Delete:
        this.deleteFilter(smartFilter);
        break;
    }
  }

  async deleteFilter(f: SmartFilter) {
    await this.actionService.deleteFilter(f.id, success => {
      if (!success) return;
      this.resetFilter();
      this.loadData();
    });
  }

  editFilter(f: SmartFilter) {
    const modalRef = this.modelService.open(EditSmartFilterModalComponent, {  size: 'xl', fullscreen: 'md' });
    modalRef.componentInstance.smartFilter = f;
    modalRef.componentInstance.allFilters = this.filters;
    modalRef.closed.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((result) => {
      if (result) {
        this.resetFilter();
        this.loadData();
      }
    });
  }

}
