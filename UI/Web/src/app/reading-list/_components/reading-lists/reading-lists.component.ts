import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  EventEmitter,
  inject,
  OnInit,
  signal,
  TemplateRef,
  viewChild,
} from '@angular/core';
import {JumpKey} from 'src/app/_models/jumpbar/jump-key';
import {PaginatedResult, Pagination} from 'src/app/_models/pagination';
import {ReadingList} from 'src/app/_models/reading-list/reading-list';
import {AccountService} from 'src/app/_services/account.service';
import {ActionService} from 'src/app/_services/action.service';
import {JumpbarService} from 'src/app/_services/jumpbar.service';
import {ReadingListService} from 'src/app/_services/reading-list.service';
import {CardDetailLayoutComponent} from '../../../cards/card-detail-layout/card-detail-layout.component';
import {DecimalPipe} from '@angular/common';
import {
  SideNavCompanionBarComponent
} from '../../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';
import {TranslocoDirective} from "@jsverse/transloco";
import {CardActionablesComponent} from "../../../_single-module/card-actionables/card-actionables.component";
import {WikiLink} from "../../../_models/wiki";
import {BulkSelectionService} from "../../../cards/bulk-selection.service";
import {BulkOperationsComponent} from "../../../cards/bulk-operations/bulk-operations.component";
import {User} from "../../../_models/user/user";
import {CardEntity, CardEntityFactory, ReadingListCardEntity} from "../../../_models/card/card-entity";
import {CardConfigFactory} from "../../../_services/card-config-factory.service";
import {ActionItem} from "../../../_models/actionables/action-item";
import {Action} from "../../../_models/actionables/action";
import {ActionResult} from "../../../_models/actionables/action-result";
import {FilterEvent} from "../../../_models/metadata/series-filter";
import {ReadingListSortField} from "../../../_models/metadata/v2/reading-list-sort-field";
import {ReadingListFilterField} from "../../../_models/metadata/v2/reading-list-filter-field";
import {FilterUtilitiesService} from "../../../shared/_services/filter-utilities.service";
import {FilterV2} from "../../../_models/metadata/v2/filter-v2";
import {ReadingListFilterSettings} from "../../../metadata-filter/filter-settings";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {FilterStatement} from "../../../_models/metadata/v2/filter-statement";
import {ActivatedRoute} from "@angular/router";
import {MetadataService} from "../../../_services/metadata.service";
import {UtilityService} from "../../../shared/_services/utility.service";
import {ReadingListComponent} from "../reading-list/reading-list.component";

@Component({
  selector: 'app-reading-lists',
  templateUrl: './reading-lists.component.html',
  styleUrls: ['./reading-lists.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SideNavCompanionBarComponent, CardActionablesComponent, CardDetailLayoutComponent, DecimalPipe,
    TranslocoDirective, BulkOperationsComponent, ReadingListComponent]
})
export class ReadingListsComponent implements OnInit {
  private readonly readingListService = inject(ReadingListService);
  private readonly accountService = inject(AccountService);
  private readonly jumpbarService = inject(JumpbarService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly cardConfigFactory = inject(CardConfigFactory);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly utilityService = inject(UtilityService);
  protected readonly bulkSelectionService = inject(BulkSelectionService);
  protected readonly actionService = inject(ActionService);
  protected readonly metadataService = inject(MetadataService);

  protected titleTemplateRef = viewChild<TemplateRef<{ $implicit: CardEntity }>>('title');

  lists = signal<ReadingList[]>([]);
  listEntities = computed(() => this.lists().map(l => CardEntityFactory.readingList(l)));
  readingListConfig = computed(() => {
    return this.cardConfigFactory.forReadingList({titleRef: this.titleTemplateRef(), shouldRenderAction: this.shouldRenderReadingListAction.bind(this)});
  });
  isLoadingLists = signal<boolean>(false);
  pagination = signal<Pagination | null>(null);
  jumpbarKeys = signal<JumpKey[]>([]);
  actions: {[key: number]: Array<ActionItem<ReadingList>>} = {};
  globalActions: Array<ActionItem<any>> = []; // TODO: Why is this empty?
  trackByIdentity = (index: number, item: ReadingListCardEntity) => `${item.data.id}`;

  filterSettings = signal<ReadingListFilterSettings>(new ReadingListFilterSettings());
  filterActive = signal<boolean>(false);
  filterOpen: EventEmitter<boolean> = new EventEmitter();
  refresh: EventEmitter<void> = new EventEmitter();
  filter: FilterV2<ReadingListFilterField, ReadingListSortField> | undefined = undefined;
  filterActiveCheck!: FilterV2<ReadingListFilterField>;


  constructor() {
    this.isLoadingLists.set(true);

    this.route.data.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(data => {
      this.filter = data['filter'] as FilterV2<ReadingListFilterField, ReadingListSortField>;

      if (this.filter == null) {
        this.filter = this.metadataService.createDefaultFilterDto('readinglist');
        this.filter.statements.push(this.metadataService.createDefaultFilterStatement('readinglist') as FilterStatement<ReadingListFilterField>);
      }

      this.filterActiveCheck = this.filterUtilityService.createReadingListV2Filter();
      const d = this.filterSettings();
      this.filterSettings.set({...d, presetsV2: this.filter});

      this.loadPage();
    });
  }

  ngOnInit(): void {
    this.bulkSelectionService.registerDataSource('readingList', () => this.lists());
    this.bulkSelectionService.registerPostAction((res: ActionResult<ReadingList>) => {
      if (res.effect === 'none') return;
      this.loadPage();
    });
  }

  performGlobalAction(event: ActionItem<void> | ActionResult<void>) {
    // Skip ActionResults - they've already been handled
    if ('effect' in event) return;
  }

  updateReadingList(updatedEntity: ReadingList) {
    const originalEntity = this.lists().find(s => s.id == updatedEntity.id);
    if (originalEntity) {
      Object.assign(originalEntity, updatedEntity);
      this.lists.set([...this.lists()]);
    }
  }

  loadPage() {
    this.filterActive.set(!this.utilityService.deepEqual(this.filter, this.filterActiveCheck));
    this.isLoadingLists.set(true);

    this.readingListService.getAllReadingLists(this.filter!).subscribe((readingLists: PaginatedResult<ReadingList[]>) => {
      this.lists.set(readingLists.result);
      this.pagination.set(readingLists.pagination);
      this.jumpbarKeys.set(this.jumpbarService.getJumpKeys(readingLists.result, (rl: ReadingList) => rl.title));
      this.isLoadingLists.set(false);
    });
  }

  shouldRenderReadingListAction(action: ActionItem<ReadingList>, entity: ReadingList, user: User) {
    const isPromoteAction = action.action === Action.Promote || action.action === Action.UnPromote;
    const hasPromotionAbility = this.accountService.hasAdminRole() || this.accountService.hasPromoteRole();

    if (isPromoteAction && !hasPromotionAbility) {
      return false;
    }

    switch (action.action) {
      case Action.Promote:
        return !entity.promoted;
      case Action.UnPromote:
        return entity.promoted;
      default:
        return true;
    }
  }

  updateFilter(data: FilterEvent<ReadingListFilterField, ReadingListSortField>) {
    if (data.filterV2 === undefined) return;
    this.filter = data.filterV2;

    if (data.isFirst) {
      return;
    }

    this.filterUtilityService.updateUrlFromFilter(this.filter).subscribe((_) => {
      this.loadPage();
    });
  }

  protected readonly WikiLink = WikiLink;
}
