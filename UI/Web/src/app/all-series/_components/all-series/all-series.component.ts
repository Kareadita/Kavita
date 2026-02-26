import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  EventEmitter,
  inject,
  OnInit
} from '@angular/core';
import {ActivatedRoute, Router} from '@angular/router';
import {debounceTime} from 'rxjs/operators';
import {BulkSelectionService} from 'src/app/cards/bulk-selection.service';
import {FilterUtilitiesService} from 'src/app/shared/_services/filter-utilities.service';
import {UtilityService} from 'src/app/shared/_services/utility.service';
import {JumpKey} from 'src/app/_models/jumpbar/jump-key';
import {Pagination} from 'src/app/_models/pagination';
import {Series} from 'src/app/_models/series';
import {FilterEvent, SortField} from 'src/app/_models/metadata/series-filter';
import {JumpbarService} from 'src/app/_services/jumpbar.service';
import {EVENTS, Message, MessageHubService} from 'src/app/_services/message-hub.service';
import {SeriesService} from 'src/app/_services/series.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {SeriesCardComponent} from '../../../cards/series-card/series-card.component';
import {CardDetailLayoutComponent} from '../../../cards/card-detail-layout/card-detail-layout.component';
import {BulkOperationsComponent} from '../../../cards/bulk-operations/bulk-operations.component';
import {DecimalPipe} from '@angular/common';
import {
  SideNavCompanionBarComponent
} from '../../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {FilterV2} from "../../../_models/metadata/v2/filter-v2";
import {FilterComparison} from "../../../_models/metadata/v2/filter-comparison";
import {BrowseTitlePipe} from "../../../_pipes/browse-title.pipe";
import {MetadataService} from "../../../_services/metadata.service";
import {Observable} from "rxjs";
import {FilterField} from "../../../_models/metadata/v2/filter-field";
import {SeriesFilterSettings} from "../../../metadata-filter/filter-settings";
import {FilterStatement} from "../../../_models/metadata/v2/filter-statement";
import {Select2Option} from "ng-select2-component";
import {KavitaTitleStrategy} from "../../../_services/kavita-title.strategy";


@Component({
  selector: 'app-all-series',
  templateUrl: './all-series.component.html',
  styleUrls: ['./all-series.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SideNavCompanionBarComponent, BulkOperationsComponent, CardDetailLayoutComponent, SeriesCardComponent,
    DecimalPipe, TranslocoDirective],
})
export class AllSeriesComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);
  private readonly seriesService = inject(SeriesService);
  private readonly kavitaTitleStrategy = inject(KavitaTitleStrategy);
  private readonly hubService = inject(MessageHubService);
  private readonly utilityService = inject(UtilityService);
  private readonly route = inject(ActivatedRoute);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly jumpbarService = inject(JumpbarService);
  private readonly cdRef = inject(ChangeDetectorRef);
  protected readonly bulkSelectionService = inject(BulkSelectionService);
  protected readonly metadataService = inject(MetadataService);

  title: string = translate('side-nav.all-series');
  series: Series[] = [];
  loadingSeries = false;
  pagination: Pagination = new Pagination();
  filter: FilterV2<FilterField, SortField> | undefined = undefined;
  filterSettings: SeriesFilterSettings = new SeriesFilterSettings();
  filterOpen: EventEmitter<boolean> = new EventEmitter();
  filterActiveCheck!: FilterV2<FilterField>;
  filterActive: boolean = false;
  jumpbarKeys: Array<JumpKey> = [];
  browseTitlePipe = new BrowseTitlePipe();


  constructor() {
    this.router.routeReuseStrategy.shouldReuseRoute = () => false;

    this.bulkSelectionService.registerDataSource('series', () => this.series);
    this.bulkSelectionService.registerPostAction(res => {
      if (res.effect === 'none') return;

      this.loadPage();
    })

    this.route.data.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(data => {
      this.filter = data['filter'] as FilterV2<FilterField, SortField>;

      if (this.filter == null) {
        this.filter = this.metadataService.createDefaultFilterDto('series');
        this.filter.statements.push(this.metadataService.createDefaultFilterStatement('series') as FilterStatement<FilterField>);
      }

      this.title = this.route.snapshot.queryParamMap.get('title') || this.filter!.name || this.title;
      this.kavitaTitleStrategy.setFormattedTitle(this.title);

      // To provide a richer experience, when we are browsing just a Genre/Tag/etc, we regenerate the title (if not explicitly passed) to "Browse {GenreName}"
      if (this.shouldRewriteTitle()) {
        const field = this.filter!.statements[0].field;

        // This api returns value as string and number, it will complain without the casting
        (this.metadataService.getOptionsForFilterField<FilterField>(field, 'series') as Observable<Select2Option[]>).subscribe((opts: Select2Option[]) => {

          const matchingOpts = opts.filter(m => `${m.value}` === `${this.filter!.statements[0].value}`);
          if (matchingOpts.length === 0) return;

          const value = matchingOpts[0].label;
          const newTitle = this.browseTitlePipe.transform(field, value);
          if (newTitle !== '') {
            this.title = newTitle;
            this.kavitaTitleStrategy.setFormattedTitle(this.title);
            this.cdRef.markForCheck();
          }
        });

      }

      this.filterActiveCheck = this.metadataService.createDefaultFilterDto('series');
      this.filterActiveCheck.statements.push(this.metadataService.createDefaultFilterStatement('series') as FilterStatement<FilterField>);
      this.filterSettings.presetsV2 = this.filter;

      this.cdRef.markForCheck();
    });
  }

  ngOnInit(): void {
    this.hubService.messages$.pipe(debounceTime(6000), takeUntilDestroyed(this.destroyRef)).subscribe((event: Message<any>) => {
      if (event.event !== EVENTS.SeriesAdded) return;
      this.loadPage();
    });
  }

  shouldRewriteTitle() {
    return this.title === translate('side-nav.all-series') && this.filter && this.filter.statements.length === 1 && this.filter.statements[0].comparison === FilterComparison.Equal
  }

  updateFilter(data: FilterEvent<FilterField, SortField>) {
    if (data.filterV2 === undefined) return;
    this.filter = data.filterV2;

    if (data.isFirst) {
      this.loadPage();
      return;
    }

    this.filterUtilityService.updateUrlFromFilter(this.filter).subscribe((_) => {
      this.loadPage();
    });
  }

  loadPage() {
    this.filterActive = !this.utilityService.deepEqual(this.filter, this.filterActiveCheck);
    this.loadingSeries = true;

    let filterName = this.route.snapshot.queryParamMap.get('name');
    filterName = filterName ? filterName.split('�')[0] : null;

    this.title = this.route.snapshot.queryParamMap.get('title') || filterName || this.filter?.name || translate('all-series.title');
    this.cdRef.markForCheck();
    this.seriesService.getAllSeriesV2(undefined, undefined, this.filter!).subscribe(series => {
      this.series = series.result;
      this.jumpbarKeys = this.jumpbarService.getJumpKeys(this.series, (s: Series) => s.sortName ?? s.name);
      this.pagination = series.pagination;
      this.loadingSeries = false;
      this.cdRef.markForCheck();
    });
  }

  updateSeries(updatedSeries: Series) {
    const originalEntity = this.series.find(s => s.id == updatedSeries.id);

    if (originalEntity) {
      Object.assign(originalEntity, updatedSeries);
      this.cdRef.markForCheck();
    }
  }

  trackByIdentity = (_: number, item: Series) => `${item.name}_${item.localizedName}_${item.pagesRead}_${item.libraryId}`;
}
