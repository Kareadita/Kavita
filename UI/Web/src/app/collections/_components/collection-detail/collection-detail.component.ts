import {DOCUMENT, NgIf, NgStyle} from '@angular/common';
import {
  AfterContentChecked,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  ElementRef,
  EventEmitter,
  HostListener,
  inject,
  Inject,
  OnInit,
  ViewChild
} from '@angular/core';
import {Title} from '@angular/platform-browser';
import {ActivatedRoute, Router} from '@angular/router';
import {NgbModal} from '@ng-bootstrap/ng-bootstrap';
import {ToastrService} from 'ngx-toastr';
import {debounceTime, take} from 'rxjs/operators';
import {BulkSelectionService} from 'src/app/cards/bulk-selection.service';
import {EditCollectionTagsComponent} from 'src/app/cards/_modals/edit-collection-tags/edit-collection-tags.component';
import {FilterSettings} from 'src/app/metadata-filter/filter-settings';
import {FilterUtilitiesService} from 'src/app/shared/_services/filter-utilities.service';
import {KEY_CODES, UtilityService} from 'src/app/shared/_services/utility.service';
import {CollectionTag} from 'src/app/_models/collection-tag';
import {SeriesAddedToCollectionEvent} from 'src/app/_models/events/series-added-to-collection-event';
import {JumpKey} from 'src/app/_models/jumpbar/jump-key';
import {Pagination} from 'src/app/_models/pagination';
import {Series} from 'src/app/_models/series';
import {FilterEvent} from 'src/app/_models/metadata/series-filter';
import {Action, ActionFactoryService, ActionItem} from 'src/app/_services/action-factory.service';
import {ActionService} from 'src/app/_services/action.service';
import {CollectionTagService} from 'src/app/_services/collection-tag.service';
import {ImageService} from 'src/app/_services/image.service';
import {JumpbarService} from 'src/app/_services/jumpbar.service';
import {EVENTS, MessageHubService} from 'src/app/_services/message-hub.service';
import {ScrollService} from 'src/app/_services/scroll.service';
import {SeriesService} from 'src/app/_services/series.service';
import {SeriesCardComponent} from '../../../cards/series-card/series-card.component';
import {CardDetailLayoutComponent} from '../../../cards/card-detail-layout/card-detail-layout.component';
import {BulkOperationsComponent} from '../../../cards/bulk-operations/bulk-operations.component';
import {ReadMoreComponent} from '../../../shared/read-more/read-more.component';
import {ImageComponent} from '../../../shared/image/image.component';

import {
  SideNavCompanionBarComponent
} from '../../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {TranslocoDirective, TranslocoService} from "@ngneat/transloco";
import {CardActionablesComponent} from "../../../_single-module/card-actionables/card-actionables.component";
import {FilterField} from "../../../_models/metadata/v2/filter-field";
import {FilterComparison} from "../../../_models/metadata/v2/filter-comparison";
import {SeriesFilterV2} from "../../../_models/metadata/v2/series-filter-v2";

@Component({
    selector: 'app-collection-detail',
    templateUrl: './collection-detail.component.html',
    styleUrls: ['./collection-detail.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [NgIf, SideNavCompanionBarComponent, CardActionablesComponent, NgStyle, ImageComponent, ReadMoreComponent, BulkOperationsComponent, CardDetailLayoutComponent, SeriesCardComponent, TranslocoDirective]
})
export class CollectionDetailComponent implements OnInit, AfterContentChecked {

  @ViewChild('scrollingBlock') scrollingBlock: ElementRef<HTMLDivElement> | undefined;
  @ViewChild('companionBar') companionBar: ElementRef<HTMLDivElement> | undefined;

  destroyRef = inject(DestroyRef);
  translocoService = inject(TranslocoService);

  collectionTag!: CollectionTag;
  isLoading: boolean = true;
  series: Array<Series> = [];
  pagination!: Pagination;
  collectionTagActions: ActionItem<CollectionTag>[] = [];
  filter: SeriesFilterV2 | undefined = undefined;
  filterSettings: FilterSettings = new FilterSettings();
  summary: string = '';

  actionInProgress: boolean = false;
  filterActiveCheck!: SeriesFilterV2;
  filterActive: boolean = false;

  jumpbarKeys: Array<JumpKey> = [];

  filterOpen: EventEmitter<boolean> = new EventEmitter();
  trackByIdentity = (index: number, item: Series) => `${item.name}_${item.localizedName}_${item.pagesRead}`;


  bulkActionCallback = (action: ActionItem<any>, data: any) => {
    const selectedSeriesIndices = this.bulkSelectionService.getSelectedCardsForSource('series');
    const selectedSeries = this.series.filter((series, index: number) => selectedSeriesIndices.includes(index + ''));

    switch (action.action) {
      case Action.AddToReadingList:
        this.actionService.addMultipleSeriesToReadingList(selectedSeries, (success) => {
          if (success) this.bulkSelectionService.deselectAll();
          this.cdRef.markForCheck();
        });
        break;
      case Action.AddToWantToReadList:
        this.actionService.addMultipleSeriesToWantToReadList(selectedSeries.map(s => s.id), () => {
          this.bulkSelectionService.deselectAll();
          this.cdRef.markForCheck();
        });
        break;
      case Action.RemoveFromWantToReadList:
        this.actionService.removeMultipleSeriesFromWantToReadList(selectedSeries.map(s => s.id), () => {
          this.bulkSelectionService.deselectAll();
          this.cdRef.markForCheck();
        });
        break;
      case Action.AddToCollection:
        this.actionService.addMultipleSeriesToCollectionTag(selectedSeries, (success) => {
          if (success) this.bulkSelectionService.deselectAll();
          this.cdRef.markForCheck();
        });
        break;
      case Action.MarkAsRead:
        this.actionService.markMultipleSeriesAsRead(selectedSeries, () => {
          this.bulkSelectionService.deselectAll();
          this.loadPage();
          this.cdRef.markForCheck();
        });
        break;
      case Action.MarkAsUnread:
        this.actionService.markMultipleSeriesAsUnread(selectedSeries, () => {
          this.bulkSelectionService.deselectAll();
          this.loadPage();
          this.cdRef.markForCheck();
        });
        break;
      case Action.Delete:
        this.actionService.deleteMultipleSeries(selectedSeries, successful => {
          if (!successful) return;
          this.bulkSelectionService.deselectAll();
          this.loadPage();
          this.cdRef.markForCheck();
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
    private modalService: NgbModal, private titleService: Title, private jumpbarService: JumpbarService,
    public bulkSelectionService: BulkSelectionService, private actionService: ActionService, private messageHub: MessageHubService,
    private filterUtilityService: FilterUtilitiesService, private utilityService: UtilityService, @Inject(DOCUMENT) private document: Document,
    private readonly cdRef: ChangeDetectorRef, private scrollService: ScrollService) {
      this.router.routeReuseStrategy.shouldReuseRoute = () => false;

      const routeId = this.route.snapshot.paramMap.get('id');
      if (routeId === null) {
        this.router.navigate(['collections']);
        return;
      }
      const tagId = parseInt(routeId, 10);

      this.pagination = this.filterUtilityService.pagination(this.route.snapshot);

      this.filter = this.filterUtilityService.filterPresetsFromUrlV2(this.route.snapshot);
      if (this.filter.statements.filter(stmt => stmt.field === FilterField.Libraries).length === 0) {
        this.filter!.statements.push({field: FilterField.CollectionTags, value: tagId + '', comparison: FilterComparison.Equal});
      }
      this.filterActiveCheck = this.filterUtilityService.createSeriesV2Filter();
      this.filterActiveCheck!.statements.push({field: FilterField.CollectionTags, value: tagId + '', comparison: FilterComparison.Equal});
      this.filterSettings.presetsV2 =  this.filter;

      this.cdRef.markForCheck();

      this.updateTag(tagId);
  }

  ngOnInit(): void {
    this.collectionTagActions = this.actionFactoryService.getCollectionTagActions(this.handleCollectionActionCallback.bind(this));

    this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef), debounceTime(2000)).subscribe(event => {
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

  ngAfterContentChecked(): void {
    this.scrollService.setScrollContainer(this.scrollingBlock);
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
        this.toastr.error(this.translocoService.translate('errors.collection-invalid-access'));
        // TODO: Why would access need to be checked? Even if a id was guessed, the series wouldn't return
        this.router.navigateByUrl('/');
        return;
      }

      this.collectionTag = matchingTags[0];
      this.summary = (this.collectionTag.summary === null ? '' : this.collectionTag.summary).replace(/\n/g, '<br>');
      this.titleService.setTitle(this.translocoService.translate('collection-detail.title-alt', {collectionName: this.collectionTag.title}));
      this.cdRef.markForCheck();
    });
  }

  loadPage() {
    this.filterActive = !this.utilityService.deepEqual(this.filter, this.filterActiveCheck);
    this.isLoading = true;
    this.cdRef.markForCheck();

    this.seriesService.getAllSeriesV2(undefined, undefined, this.filter).pipe(take(1)).subscribe(series => {
      this.series = series.result;
      this.pagination = series.pagination;
      this.jumpbarKeys = this.jumpbarService.getJumpKeys(this.series, (series: Series) => series.name);
      this.isLoading = false;
      window.scrollTo(0, 0);
      this.cdRef.markForCheck();
    });
  }

  updateFilter(data: FilterEvent) {
    if (data.filterV2 === undefined) return;
    this.filter = data.filterV2;

    if (!data.isFirst) {
      this.filterUtilityService.updateUrlFromFilterV2(this.pagination, this.filter);
    }

    this.loadPage();
  }

  handleCollectionActionCallback(action: ActionItem<CollectionTag>, collectionTag: CollectionTag) {
    switch (action.action) {
      case(Action.Edit):
        this.openEditCollectionTagModal(this.collectionTag);
        break;
      default:
        break;
    }
  }

  performAction(action: ActionItem<any>) {
    if (typeof action.callback === 'function') {
      action.callback(action, this.collectionTag);
    }
  }

  openEditCollectionTagModal(collectionTag: CollectionTag) {
    const modalRef = this.modalService.open(EditCollectionTagsComponent, { size: 'lg', scrollable: true });
    modalRef.componentInstance.tag = this.collectionTag;
    modalRef.closed.subscribe((results: {success: boolean, coverImageUpdated: boolean}) => {
      this.updateTag(this.collectionTag.id);
      this.loadPage();
    });
  }

}
