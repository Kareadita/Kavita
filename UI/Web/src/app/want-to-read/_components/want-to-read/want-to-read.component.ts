import { DOCUMENT, NgStyle, NgIf, DecimalPipe } from '@angular/common';
import {
  AfterContentChecked,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef,
  ElementRef,
  EventEmitter,
  HostListener,
  inject,
  Inject,
  OnInit,
  ViewChild
} from '@angular/core';
import { Title } from '@angular/platform-browser';
import { Router, ActivatedRoute } from '@angular/router';
import { take, debounceTime } from 'rxjs';
import { BulkSelectionService } from 'src/app/cards/bulk-selection.service';
import { FilterSettings } from 'src/app/metadata-filter/filter-settings';
import { FilterUtilitiesService } from 'src/app/shared/_services/filter-utilities.service';
import { UtilityService, KEY_CODES } from 'src/app/shared/_services/utility.service';
import { SeriesRemovedEvent } from 'src/app/_models/events/series-removed-event';
import { JumpKey } from 'src/app/_models/jumpbar/jump-key';
import { Pagination } from 'src/app/_models/pagination';
import { Series } from 'src/app/_models/series';
import { FilterEvent } from 'src/app/_models/metadata/series-filter';
import { Action, ActionItem } from 'src/app/_services/action-factory.service';
import { ActionService } from 'src/app/_services/action.service';
import { ImageService } from 'src/app/_services/image.service';
import { JumpbarService } from 'src/app/_services/jumpbar.service';
import { MessageHubService, EVENTS } from 'src/app/_services/message-hub.service';
import { ScrollService } from 'src/app/_services/scroll.service';
import { SeriesService } from 'src/app/_services/series.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { SeriesCardComponent } from '../../../cards/series-card/series-card.component';
import { CardDetailLayoutComponent } from '../../../cards/card-detail-layout/card-detail-layout.component';
import { BulkOperationsComponent } from '../../../cards/bulk-operations/bulk-operations.component';
import { SideNavCompanionBarComponent } from '../../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {SeriesFilterV2} from "../../../_models/metadata/v2/series-filter-v2";


@Component({
    selector: 'app-want-to-read',
    templateUrl: './want-to-read.component.html',
    styleUrls: ['./want-to-read.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
    imports: [SideNavCompanionBarComponent, NgStyle, BulkOperationsComponent, CardDetailLayoutComponent, SeriesCardComponent, DecimalPipe, TranslocoDirective]
})
export class WantToReadComponent implements OnInit, AfterContentChecked {

  @ViewChild('scrollingBlock') scrollingBlock: ElementRef<HTMLDivElement> | undefined;
  @ViewChild('companionBar') companionBar: ElementRef<HTMLDivElement> | undefined;
  private readonly destroyRef = inject(DestroyRef);

  isLoading: boolean = true;
  series: Array<Series> = [];
  pagination: Pagination = new Pagination();
  filter: SeriesFilterV2 | undefined = undefined;
  filterSettings: FilterSettings = new FilterSettings();
  refresh: EventEmitter<void> = new EventEmitter();

  filterActiveCheck!: SeriesFilterV2;
  filterActive: boolean = false;

  jumpbarKeys: Array<JumpKey> = [];

  filterOpen: EventEmitter<boolean> = new EventEmitter();

  trackByIdentity = (index: number, item: Series) => `${item.name}_${item.localizedName}_${item.pagesRead}`;

  bulkActionCallback = (action: ActionItem<any>, data: any) => {
    const selectedSeriesIndices = this.bulkSelectionService.getSelectedCardsForSource('series');
    const selectedSeries = this.series.filter((series, index: number) => selectedSeriesIndices.includes(index + ''));

    switch (action.action) {
      case Action.RemoveFromWantToReadList:
        this.actionService.removeMultipleSeriesFromWantToReadList(selectedSeries.map(s => s.id), () => {
          this.bulkSelectionService.deselectAll();
          this.loadPage();
        });
        break;
    }
  }

  collectionTag: any;

  get ScrollingBlockHeight() {
    if (this.scrollingBlock === undefined) return 'calc(var(--vh)*100)';
    const navbar = this.document.querySelector('.navbar') as HTMLElement;
    if (navbar === null) return 'calc(var(--vh)*100)';

    const companionHeight = this.companionBar!.nativeElement.offsetHeight;
    const navbarHeight = navbar.offsetHeight;
    const totalHeight = companionHeight + navbarHeight + 21; //21px to account for padding
    return 'calc(var(--vh)*100 - ' + totalHeight + 'px)';
  }

  constructor(public imageService: ImageService, private router: Router, private route: ActivatedRoute,
    private seriesService: SeriesService, private titleService: Title,
    public bulkSelectionService: BulkSelectionService, private actionService: ActionService, private messageHub: MessageHubService,
    private filterUtilityService: FilterUtilitiesService, private utilityService: UtilityService, @Inject(DOCUMENT) private document: Document,
    private readonly cdRef: ChangeDetectorRef, private scrollService: ScrollService, private hubService: MessageHubService,
    private jumpbarService: JumpbarService) {
      this.router.routeReuseStrategy.shouldReuseRoute = () => false;
      this.titleService.setTitle('Kavita - ' + translate('want-to-read.title'));

      this.filterUtilityService.filterPresetsFromUrl(this.route.snapshot).subscribe(filter => {
        this.filter = filter;

        this.filterActiveCheck = this.filterUtilityService.createSeriesV2Filter();
        this.filterActiveCheck!.statements.push(this.filterUtilityService.createSeriesV2DefaultStatement());
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
    this.scrollService.setScrollContainer(this.scrollingBlock);
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

    this.seriesService.getWantToRead(undefined, undefined, this.filter).pipe(take(1)).subscribe(paginatedList => {
      this.series = paginatedList.result;
      this.pagination = paginatedList.pagination;
      this.jumpbarKeys = this.jumpbarService.getJumpKeys(this.series, (series: Series) => series.name);
      this.isLoading = false;
      this.cdRef.markForCheck();
    });
  }

  updateFilter(data: FilterEvent) {
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


