import {DatePipe} from '@angular/common';
import {
  AfterContentChecked,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  EventEmitter,
  inject,
  input,
  numberAttribute,
  OnInit,
  ViewChild
} from '@angular/core';
import {Title} from '@angular/platform-browser';
import {ActivatedRoute, Router} from '@angular/router';
import {NgbOffcanvas, NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {ToastrService} from 'ngx-toastr';
import {debounceTime} from 'rxjs/operators';
import {BulkSelectionService} from 'src/app/cards/bulk-selection.service';
import {
  EditCollectionTagsModalComponent
} from 'src/app/cards/_modals/edit-collection-tags/edit-collection-tags-modal.component';
import {FilterUtilitiesService} from 'src/app/shared/_services/filter-utilities.service';
import {UtilityService} from 'src/app/shared/_services/utility.service';
import {UserCollection} from 'src/app/_models/collection-tag';
import {SeriesAddedToCollectionEvent} from 'src/app/_models/events/series-added-to-collection-event';
import {JumpKey} from 'src/app/_models/jumpbar/jump-key';
import {Pagination} from 'src/app/_models/pagination';
import {Series} from 'src/app/_models/series';
import {FilterEvent, SortField} from 'src/app/_models/metadata/series-filter';
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
import {FilterV2} from "../../../_models/metadata/v2/filter-v2";
import {AccountService} from "../../../_services/account.service";
import {User} from "../../../_models/user/user";
import {ScrobbleProvider} from "../../../_services/scrobbling.service";
import {DefaultDatePipe} from "../../../_pipes/default-date.pipe";
import {ProviderImagePipe} from "../../../_pipes/provider-image.pipe";
import {
  SmartCollectionDrawerComponent
} from "../../../_single-module/smart-collection-drawer/smart-collection-drawer.component";
import {ScrobbleProviderNamePipe} from "../../../_pipes/scrobble-provider-name.pipe";
import {PromotedIconComponent} from "../../../shared/_components/promoted-icon/promoted-icon.component";
import {FilterStatement} from "../../../_models/metadata/v2/filter-statement";
import {SeriesFilterSettings} from "../../../metadata-filter/filter-settings";
import {MetadataService} from "../../../_services/metadata.service";
import {FilterComparison} from "../../../_models/metadata/v2/filter-comparison";
import {Breakpoint, BreakpointService} from "../../../_services/breakpoint.service";
import {ActionFactoryService} from "../../../_services/action-factory.service";
import {ActionItem} from "../../../_models/actionables/action-item";
import {Action} from "../../../_models/actionables/action";
import {ActionResult} from "../../../_models/actionables/action-result";
import {ModalResult} from "../../../_models/modal/modal-result";
import {ModalService} from "../../../_services/modal.service";
import {getWritableResolvedData} from "../../../../libs/route-util";

@Component({
  selector: 'app-collection-detail',
  templateUrl: './collection-detail.component.html',
  styleUrls: ['./collection-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SideNavCompanionBarComponent, CardActionablesComponent, ImageComponent, ReadMoreComponent,
    BulkOperationsComponent, CardDetailLayoutComponent, SeriesCardComponent, TranslocoDirective, NgbTooltip,
    DatePipe, DefaultDatePipe, ProviderImagePipe, ScrobbleProviderNamePipe, PromotedIconComponent]
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
  private readonly modalService = inject(ModalService);
  private readonly offcanvasService = inject(NgbOffcanvas);
  private readonly titleService = inject(Title);
  private readonly jumpbarService = inject(JumpbarService);
  private readonly messageHub = inject(MessageHubService);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  protected readonly utilityService = inject(UtilityService);
  protected readonly breakpointService = inject(BreakpointService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly scrollService = inject(ScrollService);
  private readonly metadataService = inject(MetadataService);

  protected readonly ScrobbleProvider = ScrobbleProvider;

  @ViewChild('scrollingBlock') scrollingBlock: ElementRef<HTMLDivElement> | undefined;
  @ViewChild('companionBar') companionBar: ElementRef<HTMLDivElement> | undefined;

  collectionId = input(0, {transform: numberAttribute});
  collectionTag = getWritableResolvedData(this.route, 'collection');
  summary = computed(() => (this.collectionTag()?.summary ?? '').replace(/\n/g, '<br>'));

  isLoading: boolean = true;
  series: Array<Series> = [];
  pagination: Pagination = new Pagination();
  collectionTagActions: ActionItem<UserCollection>[] = [];
  filter: FilterV2<FilterField> | undefined = undefined;
  filterSettings: SeriesFilterSettings = new SeriesFilterSettings();
  user!: User;

  actionInProgress: boolean = false;
  filterActiveCheck!: FilterV2<FilterField>;
  filterActive: boolean = false;

  jumpbarKeys: Array<JumpKey> = [];

  filterOpen: EventEmitter<boolean> = new EventEmitter();
  trackByIdentity = (index: number, item: Series) => `${item.name}_${item.localizedName}_${item.pagesRead}`;

  constructor() {
      this.router.routeReuseStrategy.shouldReuseRoute = () => false;

      effect(() => {
        const tag = this.collectionTag();
        if (tag) {
          this.titleService.setTitle(this.translocoService.translate('collection-detail.title-alt', {collectionName: tag.title}));
        }
      });

      this.route.data.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(data => {
        this.filter = data['filter'] as FilterV2<FilterField, SortField>;
        const tag = this.collectionTag();
        const tagId = tag?.id ?? 0;

        const defaultStmt =  {field: FilterField.CollectionTags, value: tagId + '', comparison: FilterComparison.Equal};

        if (this.filter == null) {
          this.filter = this.metadataService.createDefaultFilterDto('series');
          this.filter.statements.push(defaultStmt);
        }

        if (this.filter.statements.filter((stmt: FilterStatement<FilterField>) => stmt.field === FilterField.CollectionTags).length === 0) {
          this.filter!.statements.push(defaultStmt);
        }

        this.filterActiveCheck = this.metadataService.createDefaultFilterDto('series');
        this.filterActiveCheck!.statements.push(defaultStmt);
        this.filterSettings.presetsV2 =  this.filter;

        this.loadPage();
        this.cdRef.markForCheck();
      });

      this.bulkSelectionService.registerDataSource('series', () => this.series);
      this.bulkSelectionService.registerPostAction((res: ActionResult<Series>) => {
        if (res.effect === 'none') return;
        this.loadPage();
      });
  }

  ngOnInit(): void {
    this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(user => {
      if (!user) return;
      this.user = user;
      this.collectionTagActions = this.actionFactoryService.getCollectionTagActions(this.shouldRenderCollection.bind(this))
        .filter(action => this.collectionService.actionListFilter(action, user));
      this.cdRef.markForCheck();
    });


    this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef), debounceTime(2000)).subscribe(event => {
      if (event.event == EVENTS.CollectionUpdated) {
        const collectionEvent = event.payload as SeriesAddedToCollectionEvent;
        if (collectionEvent.tagId === this.collectionTag()?.id) {
          this.loadPage();
        }
      } else if (event.event === EVENTS.SeriesRemoved) {
        this.loadPage();
      }
    });
  }

  shouldRenderCollection(action: ActionItem<UserCollection>, entity: UserCollection, user: User) {
    switch (action.action) {
      case Action.Promote:
        return !entity.promoted;
      case Action.UnPromote:
        return entity.promoted;
      default:
        return true;
    }
  }

  ngAfterContentChecked(): void {
    this.scrollService.setScrollContainer(this.scrollingBlock);
  }

  loadPage() {
    this.filterActive = !this.utilityService.deepEqual(this.filter, this.filterActiveCheck);
    this.isLoading = true;
    this.cdRef.markForCheck();

    this.seriesService.getAllSeriesV2(undefined, undefined, this.filter).subscribe(series => {
      this.series = series.result;
      this.pagination = series.pagination;
      this.jumpbarKeys = this.jumpbarService.getJumpKeys(this.series, (series: Series) => series.name);
      this.isLoading = false;
      this.cdRef.markForCheck();
    });
  }

  updateSeries(updatedEntity: Series) {
    const originalEntity = this.series.find(s => s.id == updatedEntity.id);
    if (originalEntity) {
      Object.assign(originalEntity, updatedEntity);
      this.series = [...this.series];
      this.cdRef.markForCheck();
    }
  }

  async handleActionCallback(result: ActionResult<UserCollection>) {
    switch (result.effect) {
      case 'update':
        this.collectionService.getCollectionById(this.collectionTag()!.id).subscribe(tag => {
          this.collectionTag.set(tag);
        });
        break;
      case 'remove':
        this.router.navigateByUrl('/collections');
        break;
      case 'reload':
        this.loadPage();
        break;
      case 'none':
        break;
    }
  }


  updateFilter(data: FilterEvent<FilterField, SortField>) {
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
        this.collectionService.promoteMultipleCollections([this.collectionTag()!.id], true).subscribe(() => {
          this.collectionTag.update(tag => ({...tag, promoted: true}));
          this.cdRef.markForCheck();
        });
        break;
      case Action.UnPromote:
        this.collectionService.promoteMultipleCollections([this.collectionTag()!.id], false).subscribe(() => {
          this.collectionTag.update(tag => ({...tag, promoted: false}));
          this.cdRef.markForCheck();
        });
        break;
      case(Action.Edit):
        this.openEditCollectionTagModal(this.collectionTag()!);
        break;
      case (Action.Delete):
        this.collectionService.deleteTag(this.collectionTag()!.id).subscribe(() => {
          this.toastr.success(translate('toasts.collection-tag-deleted'));
          this.router.navigateByUrl('collections');
        });
        break;
      default:
        break;
    }
  }

  openEditCollectionTagModal(collectionTag: UserCollection) {
    const modalRef = this.modalService.open(EditCollectionTagsModalComponent);
    modalRef.setInput('tag', this.collectionTag()!);
    modalRef.closed.subscribe((results: ModalResult<UserCollection>) => {
      this.collectionService.getCollectionById(this.collectionTag()!.id).subscribe(tag => {
        this.collectionTag.set(tag);
      });
      this.loadPage();
    });
  }

  openSyncDetailDrawer() {
    const ref = this.offcanvasService.open(SmartCollectionDrawerComponent, {position: 'end', panelClass: ''});
    ref.componentInstance.collection = this.collectionTag();
    ref.componentInstance.series = this.series;
  }

  protected readonly Breakpoint = Breakpoint;
}
