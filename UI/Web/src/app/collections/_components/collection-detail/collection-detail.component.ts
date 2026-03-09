import {DatePipe} from '@angular/common';
import {
  AfterContentChecked,
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  EventEmitter,
  inject,
  input,
  numberAttribute,
  signal,
  viewChild
} from '@angular/core';
import {ActivatedRoute, Router} from '@angular/router';
import {NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {debounceTime} from 'rxjs/operators';
import {BulkSelectionService} from 'src/app/cards/bulk-selection.service';
import {FilterUtilitiesService} from 'src/app/shared/_services/filter-utilities.service';
import {UtilityService} from 'src/app/shared/_services/utility.service';
import {UserCollection} from 'src/app/_models/collection-tag';
import {SeriesAddedToCollectionEvent} from 'src/app/_models/events/series-added-to-collection-event';
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
import {TranslocoDirective} from "@jsverse/transloco";
import {CardActionablesComponent} from "../../../_single-module/card-actionables/card-actionables.component";
import {FilterField} from "../../../_models/metadata/v2/filter-field";
import {FilterV2} from "../../../_models/metadata/v2/filter-v2";
import {AccountService} from "../../../_services/account.service";
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
import {getWritableResolvedData} from "../../../../libs/route-util";
import {User} from "../../../_models/user/user";
import {DrawerService} from "../../../_services/drawer.service";

@Component({
  selector: 'app-collection-detail',
  templateUrl: './collection-detail.component.html',
  styleUrls: ['./collection-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SideNavCompanionBarComponent, CardActionablesComponent, ImageComponent, ReadMoreComponent,
    BulkOperationsComponent, CardDetailLayoutComponent, SeriesCardComponent, TranslocoDirective, NgbTooltip,
    DatePipe, DefaultDatePipe, ProviderImagePipe, ScrobbleProviderNamePipe, PromotedIconComponent]
})
export class CollectionDetailComponent implements AfterContentChecked {
  public readonly imageService = inject(ImageService);
  public readonly bulkSelectionService = inject(BulkSelectionService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly collectionService = inject(CollectionTagService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly seriesService = inject(SeriesService);
  private readonly actionFactoryService = inject(ActionFactoryService);
  private readonly accountService = inject(AccountService);
  private readonly drawerService = inject(DrawerService);
  private readonly jumpbarService = inject(JumpbarService);
  private readonly messageHub = inject(MessageHubService);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  protected readonly utilityService = inject(UtilityService);
  protected readonly breakpointService = inject(BreakpointService);
  private readonly scrollService = inject(ScrollService);
  private readonly metadataService = inject(MetadataService);

  protected readonly ScrobbleProvider = ScrobbleProvider;

  scrollingBlock = viewChild<ElementRef<HTMLDivElement>>('scrollingBlock');
  companionBar = viewChild<ElementRef<HTMLDivElement>>('companionBar');

  collectionId = input(0, {transform: numberAttribute});
  collectionTag = getWritableResolvedData(this.route, 'collection');
  summary = computed(() => (this.collectionTag()?.summary ?? '').replace(/\n/g, '<br>'));

  user = this.accountService.currentUser;

  isLoading = signal(true);
  series = signal<Array<Series>>([]);
  pagination = signal(new Pagination());
  filter = signal<FilterV2<FilterField> | undefined>(undefined);
  filterSettings = signal(new SeriesFilterSettings());
  actionInProgress = signal(false);
  filterActive = signal(false);
  jumpbarKeys = computed(() => this.jumpbarService.getJumpKeys(this.series(), (s: Series) => s.name));

  collectionTagActions = computed(() => {
    const user = this.user();
    if (!user) return [];
    return this.actionFactoryService.getCollectionTagActions(this.shouldRenderCollection.bind(this))
      .filter(action => this.collectionService.actionListFilter(action, user));
  });

  filterActiveCheck = computed(() => {
    const tagId = this.collectionTag()?.id ?? 0;
    const check = this.metadataService.createDefaultFilterDto('series');
    check.statements.push({field: FilterField.CollectionTags, value: tagId + '', comparison: FilterComparison.Equal});
    return check;
  });

  filterOpen: EventEmitter<boolean> = new EventEmitter();
  trackByIdentity = (index: number, item: Series) => `${item.name}_${item.localizedName}_${item.pagesRead}`;

  constructor() {
      this.router.routeReuseStrategy.shouldReuseRoute = () => false;

      this.route.data.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(data => {
        let filter = data['filter'] as FilterV2<FilterField, SortField>;
        const tag = this.collectionTag();
        const tagId = tag?.id ?? 0;

        const defaultStmt =  {field: FilterField.CollectionTags, value: tagId + '', comparison: FilterComparison.Equal};

        if (filter == null) {
          filter = this.metadataService.createDefaultFilterDto('series');
          filter.statements.push(defaultStmt);
        }

        if (filter.statements.filter((stmt: FilterStatement<FilterField>) => stmt.field === FilterField.CollectionTags).length === 0) {
          filter!.statements.push(defaultStmt);
        }

        this.filter.set(filter);

        const settings = new SeriesFilterSettings();
        settings.presetsV2 = filter;
        this.filterSettings.set(settings);

        this.loadPage();
      });

      this.bulkSelectionService.registerDataSource('series', () => this.series());
      this.bulkSelectionService.registerPostAction((res: ActionResult<Series>) => {
        if (res.effect === 'none') return;
        this.loadPage();
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
    this.scrollService.setScrollContainer(this.scrollingBlock());
  }

  loadPage() {
    this.filterActive.set(!this.utilityService.deepEqual(this.filter(), this.filterActiveCheck()));
    this.isLoading.set(true);

    this.seriesService.getAllSeriesV2(undefined, undefined, this.filter()).subscribe(series => {
      this.series.set(series.result);
      this.pagination.set(series.pagination);
      this.isLoading.set(false);
    });
  }

  updateSeries(updatedEntity: Series) {
    const list = this.series();
    const originalEntity = list.find(s => s.id == updatedEntity.id);
    if (!originalEntity) return;

    Object.assign(originalEntity, updatedEntity);
    this.series.set([...list]);
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
    this.filter.set(data.filterV2);

    if (data.isFirst) {
      this.loadPage();
      return;
    }

    this.filterUtilityService.updateUrlFromFilter(this.filter()!).subscribe((encodedFilter) => {
      this.loadPage();
    });
  }

  openSyncDetailDrawer() {
    const ref = this.drawerService.open(SmartCollectionDrawerComponent, {position: 'end', panelClass: ''});
    ref.setInput('collection', this.collectionTag());
    ref.setInput('series', this.series());
  }

  protected readonly Breakpoint = Breakpoint;
}
