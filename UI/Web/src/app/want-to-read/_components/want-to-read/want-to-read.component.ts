import {DecimalPipe, DOCUMENT, NgStyle} from '@angular/common';
import {
  AfterContentChecked,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  ElementRef,
  EventEmitter,
  inject,
  OnInit,
  viewChild
} from '@angular/core';
import {ActivatedRoute, Router} from '@angular/router';
import {debounceTime} from 'rxjs';
import {BulkSelectionService} from 'src/app/cards/bulk-selection.service';
import {FilterUtilitiesService} from 'src/app/shared/_services/filter-utilities.service';
import {UtilityService} from 'src/app/shared/_services/utility.service';
import {SeriesRemovedEvent} from 'src/app/_models/events/series-removed-event';
import {JumpKey} from 'src/app/_models/jumpbar/jump-key';
import {Pagination} from 'src/app/_models/pagination';
import {Series} from 'src/app/_models/series';
import {FilterEvent, SeriesSortField} from 'src/app/_models/metadata/series-filter';
import {ImageService} from 'src/app/_services/image.service';
import {JumpbarService} from 'src/app/_services/jumpbar.service';
import {EVENTS, MessageHubService} from 'src/app/_services/message-hub.service';
import {ScrollService} from 'src/app/_services/scroll.service';
import {SeriesService} from 'src/app/_services/series.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {SeriesCardComponent} from '../../../cards/series-card/series-card.component';
import {CardDetailLayoutComponent} from '../../../cards/card-detail-layout/card-detail-layout.component';
import {BulkOperationsComponent} from '../../../cards/bulk-operations/bulk-operations.component';
import {
  SideNavCompanionBarComponent
} from '../../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';
import {TranslocoDirective} from "@jsverse/transloco";
import {FilterV2} from "../../../_models/metadata/v2/filter-v2";
import {SeriesFilterField} from "../../../_models/metadata/v2/series-filter-field";
import {SeriesFilterSettings} from "../../../metadata-filter/filter-settings";
import {MetadataService} from "../../../_services/metadata.service";
import {FilterStatement} from "../../../_models/metadata/v2/filter-statement";
import {FilterComparison} from "../../../_models/metadata/v2/filter-comparison";
import {ActionResult} from "../../../_models/actionables/action-result";


@Component({
    selector: 'app-want-to-read',
    templateUrl: './want-to-read.component.html',
    styleUrls: ['./want-to-read.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [SideNavCompanionBarComponent, NgStyle, BulkOperationsComponent, CardDetailLayoutComponent, SeriesCardComponent, DecimalPipe, TranslocoDirective]
})
export class WantToReadComponent implements OnInit, AfterContentChecked {
  protected imageService = inject(ImageService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private seriesService = inject(SeriesService);
  protected bulkSelectionService = inject(BulkSelectionService);
  private messageHub = inject(MessageHubService);
  private filterUtilityService = inject(FilterUtilitiesService);
  private utilityService = inject(UtilityService);
  private document = inject<Document>(DOCUMENT);
  private readonly cdRef = inject(ChangeDetectorRef);
  private scrollService = inject(ScrollService);
  private hubService = inject(MessageHubService);
  private jumpbarService = inject(JumpbarService);


  readonly scrollingBlock = viewChild<ElementRef<HTMLDivElement>>('scrollingBlock');
  readonly companionBar = viewChild<ElementRef<HTMLDivElement>>('companionBar');
  private readonly destroyRef = inject(DestroyRef);
  private readonly metadataService = inject(MetadataService);

  isLoading: boolean = true;
  series: Array<Series> = [];
  pagination: Pagination = new Pagination();
  filter: FilterV2<SeriesFilterField, SeriesSortField> | undefined = undefined;
  filterSettings: SeriesFilterSettings = new SeriesFilterSettings();
  refresh: EventEmitter<void> = new EventEmitter();

  filterActiveCheck!: FilterV2<SeriesFilterField>;
  filterActive: boolean = false;

  jumpbarKeys: Array<JumpKey> = [];

  filterOpen: EventEmitter<boolean> = new EventEmitter();

  trackByIdentity = (index: number, item: Series) => `${item.name}_${item.localizedName}_${item.pagesRead}`;


  get ScrollingBlockHeight() {
    if (this.scrollingBlock() === undefined) return 'calc(var(--vh)*100)';
    const navbar = this.document.querySelector('.navbar') as HTMLElement;
    if (navbar === null) return 'calc(var(--vh)*100)';

    const companionHeight = this.companionBar()!.nativeElement.offsetHeight;
    const navbarHeight = navbar.offsetHeight;
    const totalHeight = companionHeight + navbarHeight + 21; //21px to account for padding
    return 'calc(var(--vh)*100 - ' + totalHeight + 'px)';
  }

  constructor() {
      this.router.routeReuseStrategy.shouldReuseRoute = () => false;

      this.bulkSelectionService.registerDataSource('series', () => this.series);
      this.bulkSelectionService.registerPostAction((result: ActionResult<Series>) => {
        this.loadPage();
      });


      this.route.data.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(data => {
        this.filter = data['filter'] as FilterV2<SeriesFilterField, SeriesSortField>;

        const defaultStmt = {field: SeriesFilterField.WantToRead, value: 'true', comparison: FilterComparison.Equal} as FilterStatement<SeriesFilterField>;

        if (this.filter == null) {
          this.filter = this.metadataService.createDefaultFilterDto('series');
          this.filter.statements.push(defaultStmt);
        }


        this.filterActiveCheck = this.metadataService.createDefaultFilterDto('series');
        this.filterActiveCheck!.statements.push(defaultStmt);
        this.filterSettings.presetsV2 =  this.filter;


        this.cdRef.markForCheck();
      });

      this.hubService.messages$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
        if (event.event === EVENTS.SeriesRemoved) {
          const seriesRemoved = event.payload as SeriesRemovedEvent;
          if (!this.utilityService.deepEqual(this.filter, this.filterActiveCheck)) {
            this.loadPage();
            return;
          }

          this.series = this.series.filter(s => s.id != seriesRemoved.seriesId);
          this.pagination.totalItems--;
          this.cdRef.markForCheck();
          this.refresh.emit();
        }
      });

  }

  ngOnInit(): void {
    this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef), debounceTime(2000)).subscribe(event => {
      if (event.event === EVENTS.SeriesRemoved) {
        this.loadPage();
      }
    });
  }

  ngAfterContentChecked(): void {
    this.scrollService.setScrollContainer(this.scrollingBlock());
  }

  removeSeries(seriesId: number) {
    this.series = this.series.filter(s => s.id != seriesId);
    this.pagination.totalItems--;
    this.cdRef.markForCheck();
    this.refresh.emit();
  }

  loadPage() {
    this.filterActive = !this.utilityService.deepEqual(this.filter, this.filterActiveCheck);
    this.isLoading = true;
    this.cdRef.markForCheck();

    this.seriesService.getWantToRead(undefined, undefined, this.filter).subscribe(paginatedList => {
      this.series = paginatedList.result;
      this.pagination = paginatedList.pagination;
      this.jumpbarKeys = this.jumpbarService.getJumpKeys(this.series, (series: Series) => series.name);
      this.isLoading = false;
      this.cdRef.markForCheck();
    });
  }

  updateFilter(data: FilterEvent<SeriesFilterField, SeriesSortField>) {
    if (data.filterV2 === undefined) return;
    this.filter = data.filterV2;

    if (data.isFirst) {
      this.loadPage();
      return;
    }

    this.filterUtilityService.updateUrlFromFilter(this.filter).subscribe((encodedFilter) => {
      this.loadPage();
    });
  }
}


