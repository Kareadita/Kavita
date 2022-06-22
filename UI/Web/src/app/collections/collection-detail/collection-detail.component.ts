import { DOCUMENT } from '@angular/common';
import { Component, ElementRef, EventEmitter, HostListener, Inject, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { Router, ActivatedRoute } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { Subject } from 'rxjs';
import { debounceTime, take, takeUntil } from 'rxjs/operators';
import { BulkSelectionService } from 'src/app/cards/bulk-selection.service';
import { EditCollectionTagsComponent } from 'src/app/cards/_modals/edit-collection-tags/edit-collection-tags.component';
import { FilterSettings } from 'src/app/metadata-filter/filter-settings';
import { FilterUtilitiesService } from 'src/app/shared/_services/filter-utilities.service';
import { KEY_CODES, UtilityService } from 'src/app/shared/_services/utility.service';
import { CollectionTag } from 'src/app/_models/collection-tag';
import { SeriesAddedToCollectionEvent } from 'src/app/_models/events/series-added-to-collection-event';
import { JumpKey } from 'src/app/_models/jumpbar/jump-key';
import { Pagination } from 'src/app/_models/pagination';
import { Series } from 'src/app/_models/series';
import { FilterEvent, SeriesFilter } from 'src/app/_models/series-filter';
import { Action, ActionFactoryService, ActionItem } from 'src/app/_services/action-factory.service';
import { ActionService } from 'src/app/_services/action.service';
import { CollectionTagService } from 'src/app/_services/collection-tag.service';
import { ImageService } from 'src/app/_services/image.service';
import { EVENTS, MessageHubService } from 'src/app/_services/message-hub.service';
import { SeriesService } from 'src/app/_services/series.service';

@Component({
  selector: 'app-collection-detail',
  templateUrl: './collection-detail.component.html',
  styleUrls: ['./collection-detail.component.scss']
})
export class CollectionDetailComponent implements OnInit, OnDestroy {

  @ViewChild('scrollingBlock') scrollingBlock: ElementRef<HTMLDivElement> | undefined;
  @ViewChild('companionBar') companionBar: ElementRef<HTMLDivElement> | undefined;

  collectionTag!: CollectionTag;
  tagImage: string = '';
  isLoading: boolean = true;
  series: Array<Series> = [];
  seriesPagination!: Pagination;
  collectionTagActions: ActionItem<CollectionTag>[] = [];
  filter: SeriesFilter | undefined = undefined;
  filterSettings: FilterSettings = new FilterSettings();
  summary: string = '';

  actionInProgress: boolean = false;
  filterActiveCheck!: SeriesFilter;
  filterActive: boolean = false;

  jumpbarKeys: Array<JumpKey> = [];

  filterOpen: EventEmitter<boolean> = new EventEmitter();
 

  private onDestory: Subject<void> = new Subject<void>();

  bulkActionCallback = (action: Action, data: any) => {
    const selectedSeriesIndexies = this.bulkSelectionService.getSelectedCardsForSource('series');
    const selectedSeries = this.series.filter((series, index: number) => selectedSeriesIndexies.includes(index + ''));

    switch (action) {
      case Action.AddToReadingList:
        this.actionService.addMultipleSeriesToReadingList(selectedSeries, () => {
          this.bulkSelectionService.deselectAll();
        });
        break;
      case Action.AddToCollection:
        this.actionService.addMultipleSeriesToCollectionTag(selectedSeries, () => {
          this.bulkSelectionService.deselectAll();
        });
        break;
      case Action.MarkAsRead:
        this.actionService.markMultipleSeriesAsRead(selectedSeries, () => {
          this.loadPage();
          this.bulkSelectionService.deselectAll();
        });
        break;
      case Action.MarkAsUnread:
        this.actionService.markMultipleSeriesAsUnread(selectedSeries, () => {
          this.loadPage();
          this.bulkSelectionService.deselectAll();
        });
        break;
      case Action.Delete:
        this.actionService.deleteMultipleSeries(selectedSeries, () => {
          this.loadPage();
          this.bulkSelectionService.deselectAll();
        });
        break;
    }
  }

  get ScrollingBlockHeight() {
    if (this.scrollingBlock === undefined) return 'calc(var(--vh)*100)';
    const navbar = this.document.querySelector('.navbar') as HTMLElement;
    if (navbar === null) return 'calc(var(--vh)*100)';

    const companionHeight = this.companionBar!.nativeElement.offsetHeight;
    const navbarHeight = navbar.offsetHeight;
    const totalHeight = companionHeight + navbarHeight + 21; //21px to account for padding
    return 'calc(var(--vh)*100 - ' + totalHeight + 'px)';
  }

  constructor(public imageService: ImageService, private collectionService: CollectionTagService, private router: Router, private route: ActivatedRoute, 
    private seriesService: SeriesService, private toastr: ToastrService, private actionFactoryService: ActionFactoryService, 
    private modalService: NgbModal, private titleService: Title, 
    public bulkSelectionService: BulkSelectionService, private actionService: ActionService, private messageHub: MessageHubService, 
    private filterUtilityService: FilterUtilitiesService, private utilityService: UtilityService, @Inject(DOCUMENT) private document: Document) {
      this.router.routeReuseStrategy.shouldReuseRoute = () => false;

      const routeId = this.route.snapshot.paramMap.get('id');
      if (routeId === null) {
        this.router.navigate(['collections']);
        return;
      }
      const tagId = parseInt(routeId, 10);

      this.seriesPagination = this.filterUtilityService.pagination(this.route.snapshot);
      [this.filterSettings.presets, this.filterSettings.openByDefault] = this.filterUtilityService.filterPresetsFromUrl(this.route.snapshot);
      this.filterSettings.presets.collectionTags = [tagId];
      this.filterActiveCheck = this.seriesService.createSeriesFilter();
      this.filterActiveCheck.collectionTags = [tagId];
      
      this.updateTag(tagId);
  }

  ngOnInit(): void {
    this.collectionTagActions = this.actionFactoryService.getCollectionTagActions(this.handleCollectionActionCallback.bind(this));

    this.messageHub.messages$.pipe(takeUntil(this.onDestory), debounceTime(2000)).subscribe(event => {
      if (event.event == EVENTS.SeriesAddedToCollection) {
        const collectionEvent = event.payload as SeriesAddedToCollectionEvent;
        if (collectionEvent.tagId === this.collectionTag.id) {
          this.loadPage();
        }
      } else if (event.event === EVENTS.SeriesRemoved) {
        this.loadPage();
      }
    });
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

  updateTag(tagId: number) {
    this.collectionService.allTags().subscribe(tags => {
      const matchingTags = tags.filter(t => t.id === tagId);
      if (matchingTags.length === 0) {
        this.toastr.error('You don\'t have access to any libraries this tag belongs to or this tag is invalid');
        this.router.navigateByUrl('/');
        return;
      }

      this.collectionTag = matchingTags[0];
      this.summary = (this.collectionTag.summary === null ? '' : this.collectionTag.summary).replace(/\n/g, '<br>');
      this.tagImage = this.imageService.randomize(this.imageService.getCollectionCoverImage(this.collectionTag.id));
      this.titleService.setTitle('Kavita - ' + this.collectionTag.title + ' Collection');
    });
  }

  // onPageChange(pagination: Pagination) {
  //   this.filterUtilityService.updateUrlFromFilter(this.seriesPagination, undefined);
  //   this.loadPage();
  // }

  loadPage() {
    this.filterActive = !this.utilityService.deepEqual(this.filter, this.filterActiveCheck);
    this.seriesService.getAllSeries(undefined, undefined, this.filter).pipe(take(1)).subscribe(series => {
      this.series = series.result;
      this.seriesPagination = series.pagination;

      const keys: {[key: string]: number} = {};
      series.result.forEach(s => {
        let ch = s.name.charAt(0);
        if (/\d|\#|!|%|@|\(|\)|\^|\*/g.test(ch)) {
          ch = '#';
        }
        if (!keys.hasOwnProperty(ch)) {
          keys[ch] = 0;
        }
        keys[ch] += 1;
      });
      this.jumpbarKeys = Object.keys(keys).map(k => {
        return {
          key: k,
          size: keys[k],
          title: k.toUpperCase()
        }
      }).sort((a, b) => {
        if (a.key < b.key) return -1;
        if (a.key > b.key) return 1;
        return 0;
      });

      this.isLoading = false;
      window.scrollTo(0, 0);
    });
  }

  updateFilter(data: FilterEvent) {
    this.filter = data.filter;
    
    if (!data.isFirst) this.filterUtilityService.updateUrlFromFilter(this.seriesPagination, this.filter);
    this.loadPage();
  }

  handleCollectionActionCallback(action: Action, collectionTag: CollectionTag) {
    switch (action) {
      case(Action.Edit):
        this.openEditCollectionTagModal(this.collectionTag);
        break;
      default:
        break;
    }
  }

  performAction(action: ActionItem<any>) {
    if (typeof action.callback === 'function') {
      action.callback(action.action, this.collectionTag);
    }
  }

  openEditCollectionTagModal(collectionTag: CollectionTag) {
    const modalRef = this.modalService.open(EditCollectionTagsComponent, { size: 'lg', scrollable: true });
    modalRef.componentInstance.tag = this.collectionTag;
    modalRef.closed.subscribe((results: {success: boolean, coverImageUpdated: boolean}) => {
      this.updateTag(this.collectionTag.id);
      this.loadPage();
      if (results.coverImageUpdated) {
        this.tagImage = this.imageService.randomize(this.imageService.getCollectionCoverImage(collectionTag.id));
      }
    });
  }

}
