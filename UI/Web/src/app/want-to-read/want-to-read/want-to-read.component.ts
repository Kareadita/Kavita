import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, ElementRef, EventEmitter, HostListener, Inject, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { Router, ActivatedRoute } from '@angular/router';
import { Subject, take, debounceTime, takeUntil } from 'rxjs';
import { BulkSelectionService } from 'src/app/cards/bulk-selection.service';
import { FilterSettings } from 'src/app/metadata-filter/filter-settings';
import { FilterUtilitiesService } from 'src/app/shared/_services/filter-utilities.service';
import { UtilityService, KEY_CODES } from 'src/app/shared/_services/utility.service';
import { JumpKey } from 'src/app/_models/jumpbar/jump-key';
import { Pagination } from 'src/app/_models/pagination';
import { Series } from 'src/app/_models/series';
import { SeriesFilter, FilterEvent } from 'src/app/_models/series-filter';
import { Action } from 'src/app/_services/action-factory.service';
import { ActionService } from 'src/app/_services/action.service';
import { ImageService } from 'src/app/_services/image.service';
import { MessageHubService, EVENTS } from 'src/app/_services/message-hub.service';
import { ScrollService } from 'src/app/_services/scroll.service';
import { SeriesService } from 'src/app/_services/series.service';


@Component({
  selector: 'app-want-to-read',
  templateUrl: './want-to-read.component.html',
  styleUrls: ['./want-to-read.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class WantToReadComponent implements OnInit, OnDestroy {

  @ViewChild('scrollingBlock') scrollingBlock: ElementRef<HTMLDivElement> | undefined;
  @ViewChild('companionBar') companionBar: ElementRef<HTMLDivElement> | undefined;

  isLoading: boolean = true;
  series: Array<Series> = [];
  seriesPagination!: Pagination;
  filter: SeriesFilter | undefined = undefined;
  filterSettings: FilterSettings = new FilterSettings();

  filterActiveCheck!: SeriesFilter;
  filterActive: boolean = false;

  jumpbarKeys: Array<JumpKey> = [];

  filterOpen: EventEmitter<boolean> = new EventEmitter();
 

  private onDestory: Subject<void> = new Subject<void>();
  trackByIdentity = (index: number, item: Series) => `${item.name}_${item.localizedName}_${item.pagesRead}`;

  bulkActionCallback = (action: Action, data: any) => {
    const selectedSeriesIndexies = this.bulkSelectionService.getSelectedCardsForSource('series');
    const selectedSeries = this.series.filter((series, index: number) => selectedSeriesIndexies.includes(index + ''));

    switch (action) {
      case Action.RemoveFromWantToReadList:
        this.actionService.removeMultipleSeriesFromWantToReadList(selectedSeries.map(s => s.id), () => {
          this.bulkSelectionService.deselectAll();
          this.loadPage();
        });
        break;
    }
  }
  collectionTag: any;
  tagImage: any;

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
    private readonly cdRef: ChangeDetectorRef, private scrollService: ScrollService) {
      this.router.routeReuseStrategy.shouldReuseRoute = () => false;
      this.titleService.setTitle('Want To Read');

      this.seriesPagination = this.filterUtilityService.pagination(this.route.snapshot);
      [this.filterSettings.presets, this.filterSettings.openByDefault] = this.filterUtilityService.filterPresetsFromUrl(this.route.snapshot);
      this.filterActiveCheck = this.seriesService.createSeriesFilter();
      this.cdRef.markForCheck();
      
  }

  ngOnInit(): void {
    
    this.messageHub.messages$.pipe(takeUntil(this.onDestory), debounceTime(2000)).subscribe(event => {
      if (event.event === EVENTS.SeriesRemoved) {
        this.loadPage();
      }
    });
  }

  ngAfterContentChecked(): void {
    this.scrollService.setScrollContainer(this.scrollingBlock);
  }

  ngOnDestroy() {
    this.onDestory.next();
    this.onDestory.complete();
  }

  @HostListener('document:keydown.shift', ['$event'])
  handleKeypress(event: KeyboardEvent) {
    if (event.key === KEY_CODES.SHIFT) {
      this.bulkSelectionService.isShiftDown = true;
    }
  }

  @HostListener('document:keyup.shift', ['$event'])
  handleKeyUp(event: KeyboardEvent) {
    if (event.key === KEY_CODES.SHIFT) {
      this.bulkSelectionService.isShiftDown = false;
    }
  }

  loadPage() {
    this.filterActive = !this.utilityService.deepEqual(this.filter, this.filterActiveCheck);
    this.isLoading = true;
    this.cdRef.markForCheck();
    
    this.seriesService.getWantToRead(undefined, undefined, this.filter).pipe(take(1)).subscribe(paginatedList => {
      this.series = paginatedList.result;
      this.seriesPagination = paginatedList.pagination;
      this.jumpbarKeys = this.utilityService.getJumpKeys(this.series, (series: Series) => series.name);
      this.isLoading = false;
      window.scrollTo(0, 0);
      this.cdRef.markForCheck();
    });
  }

  updateFilter(data: FilterEvent) {
    this.filter = data.filter;
    
    if (!data.isFirst) this.filterUtilityService.updateUrlFromFilter(this.seriesPagination, this.filter);
    this.loadPage();
  }
}


