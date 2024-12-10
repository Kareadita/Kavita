import {AsyncPipe, DatePipe, DOCUMENT, NgStyle} from '@angular/common';
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
import {NgbModal, NgbOffcanvas, NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {ToastrService} from 'ngx-toastr';
import {debounceTime, take} from 'rxjs/operators';
import {BulkSelectionService} from 'src/app/cards/bulk-selection.service';
import {EditCollectionTagsComponent} from 'src/app/cards/_modals/edit-collection-tags/edit-collection-tags.component';
import {FilterSettings} from 'src/app/metadata-filter/filter-settings';
import {FilterUtilitiesService} from 'src/app/shared/_services/filter-utilities.service';
import {Breakpoint, KEY_CODES, UtilityService} from 'src/app/shared/_services/utility.service';
import {UserCollection} from 'src/app/_models/collection-tag';
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
import {translate, TranslocoDirective, TranslocoService} from "@jsverse/transloco";
import {CardActionablesComponent} from "../../../_single-module/card-actionables/card-actionables.component";
import {FilterField} from "../../../_models/metadata/v2/filter-field";
import {FilterComparison} from "../../../_models/metadata/v2/filter-comparison";
import {SeriesFilterV2} from "../../../_models/metadata/v2/series-filter-v2";
import {AccountService} from "../../../_services/account.service";
import {User} from "../../../_models/user";
import {ScrobbleProvider} from "../../../_services/scrobbling.service";
import {SafeHtmlPipe} from "../../../_pipes/safe-html.pipe";
import {TranslocoDatePipe} from "@jsverse/transloco-locale";
import {DefaultDatePipe} from "../../../_pipes/default-date.pipe";
import {ProviderImagePipe} from "../../../_pipes/provider-image.pipe";
import {ProviderNamePipe} from "../../../_pipes/provider-name.pipe";
import {PromotedIconComponent} from "../../../shared/_components/promoted-icon/promoted-icon.component";
import {
  SmartCollectionDrawerComponent
} from "../../../_single-module/smart-collection-drawer/smart-collection-drawer.component";
import {DefaultModalOptions} from "../../../_models/default-modal-options";

@Component({
  selector: 'app-collection-detail',
  templateUrl: './collection-detail.component.html',
  styleUrls: ['./collection-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [SideNavCompanionBarComponent, CardActionablesComponent, NgStyle, ImageComponent, ReadMoreComponent,
    BulkOperationsComponent, CardDetailLayoutComponent, SeriesCardComponent, TranslocoDirective, NgbTooltip,
    SafeHtmlPipe, TranslocoDatePipe, DatePipe, DefaultDatePipe, ProviderImagePipe, ProviderNamePipe, AsyncPipe,
    PromotedIconComponent]
})
export class CollectionDetailComponent implements OnInit, AfterContentChecked {

  public readonly imageService = inject(ImageService);
  public readonly bulkSelectionService = inject(BulkSelectionService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly translocoService = inject(TranslocoService);
  private readonly collectionService = inject(CollectionTagService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly seriesService = inject(SeriesService);
  private readonly toastr = inject(ToastrService);
  private readonly actionFactoryService = inject(ActionFactoryService);
  private readonly accountService = inject(AccountService);
  private readonly modalService = inject(NgbModal);
  private readonly offcanvasService = inject(NgbOffcanvas);
  private readonly titleService = inject(Title);
  private readonly jumpbarService = inject(JumpbarService);
  private readonly actionService = inject(ActionService);
  private readonly messageHub = inject(MessageHubService);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  protected readonly utilityService = inject(UtilityService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly scrollService = inject(ScrollService);

  protected readonly ScrobbleProvider = ScrobbleProvider;
  protected readonly Breakpoint = Breakpoint;

  @ViewChild('scrollingBlock') scrollingBlock: ElementRef<HTMLDivElement> | undefined;
  @ViewChild('companionBar') companionBar: ElementRef<HTMLDivElement> | undefined;



  collectionTag!: UserCollection;
  isLoading: boolean = true;
  series: Array<Series> = [];
  pagination: Pagination = new Pagination();
  collectionTagActions: ActionItem<UserCollection>[] = [];
  filter: SeriesFilterV2 | undefined = undefined;
  filterSettings: FilterSettings = new FilterSettings();
  summary: string = '';
  user!: User;

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

  constructor(@Inject(DOCUMENT) private document: Document) {
      this.router.routeReuseStrategy.shouldReuseRoute = () => false;

      const routeId = this.route.snapshot.paramMap.get('id');
      if (routeId === null) {
        this.router.navigate(['collections']);
        return;
      }
      const tagId = parseInt(routeId, 10);

      this.filterUtilityService.filterPresetsFromUrl(this.route.snapshot).subscribe(filter => {
        this.filter = filter;

        if (this.filter.statements.filter(stmt => stmt.field === FilterField.CollectionTags).length === 0) {
          this.filter!.statements.push({field: FilterField.CollectionTags, value: tagId + '', comparison: FilterComparison.Equal});
        }
        this.filterActiveCheck = this.filterUtilityService.createSeriesV2Filter();
        this.filterActiveCheck!.statements.push({field: FilterField.CollectionTags, value: tagId + '', comparison: FilterComparison.Equal});
        this.filterSettings.presetsV2 =  this.filter;
        this.cdRef.markForCheck();

        this.updateTag(tagId);
      });
  }

  ngOnInit(): void {
    this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(user => {
      if (!user) return;
      this.user = user;
      this.collectionTagActions = this.actionFactoryService.getCollectionTagActions(this.handleCollectionActionCallback.bind(this))
        .filter(action => this.collectionService.actionListFilter(action, user));
      this.cdRef.markForCheck();
    });


    this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef), debounceTime(2000)).subscribe(event => {
      if (event.event == EVENTS.CollectionUpdated) {
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

  updateTag(tagId: number) {
    this.collectionService.allCollections().subscribe(tags => {
      const matchingTags = tags.filter(t => t.id === tagId);
      if (matchingTags.length === 0) {
        this.toastr.error(this.translocoService.translate('errors.collection-invalid-access'));
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

  handleCollectionActionCallback(action: ActionItem<UserCollection>, collectionTag: UserCollection) {
    if (collectionTag.owner != this.user.username) {
      this.toastr.error(translate('toasts.collection-not-owned'));
      return;
    }
    switch (action.action) {
      case Action.Promote:
        this.collectionService.promoteMultipleCollections([this.collectionTag.id], true).subscribe(() => {
          this.collectionTag.promoted = true;
          this.cdRef.markForCheck();
        });
        break;
      case Action.UnPromote:
        this.collectionService.promoteMultipleCollections([this.collectionTag.id], false).subscribe(() => {
          this.collectionTag.promoted = false;
          this.cdRef.markForCheck();
        });
        break;
      case(Action.Edit):
        this.openEditCollectionTagModal(this.collectionTag);
        break;
      case (Action.Delete):
        this.collectionService.deleteTag(this.collectionTag.id).subscribe(() => {
          this.toastr.success(translate('toasts.collection-tag-deleted'));
          this.router.navigateByUrl('collections');
        });
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

  openEditCollectionTagModal(collectionTag: UserCollection) {
    const modalRef = this.modalService.open(EditCollectionTagsComponent, DefaultModalOptions);
    modalRef.componentInstance.tag = this.collectionTag;
    modalRef.closed.subscribe((results: {success: boolean, coverImageUpdated: boolean}) => {
      this.updateTag(this.collectionTag.id);
      this.loadPage();
    });
  }

  openSyncDetailDrawer() {

    const ref = this.offcanvasService.open(SmartCollectionDrawerComponent, {position: 'end', panelClass: ''});
    ref.componentInstance.collection = this.collectionTag;
    ref.componentInstance.series = this.series;
  }


}
