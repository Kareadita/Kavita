import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import {JumpKey} from 'src/app/_models/jumpbar/jump-key';
import {PaginatedResult, Pagination} from 'src/app/_models/pagination';
import {ReadingList} from 'src/app/_models/reading-list';
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
import {CardEntityFactory, ReadingListCardEntity} from "../../../_models/card/card-entity";
import {ReadingListComponent} from "../reading-list/reading-list.component";
import {CardConfigFactory} from "../../../_services/card-config-factory.service";
import {ActionItem} from "../../../_models/actionables/action-item";
import {Action} from "../../../_models/actionables/action";
import {ActionResult} from "../../../_models/actionables/action-result";
@Component({
  selector: 'app-reading-lists',
  templateUrl: './reading-lists.component.html',
  styleUrls: ['./reading-lists.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SideNavCompanionBarComponent, CardActionablesComponent, CardDetailLayoutComponent, DecimalPipe,
    TranslocoDirective, BulkOperationsComponent, ReadingListComponent]
})
export class ReadingListsComponent implements OnInit {
  private readingListService = inject(ReadingListService);
  private readonly accountService = inject(AccountService);
  private readonly jumpbarService = inject(JumpbarService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly cardConfigFactory = inject(CardConfigFactory);
  protected readonly bulkSelectionService = inject(BulkSelectionService);
  protected readonly actionService = inject(ActionService);

  protected readonly WikiLink = WikiLink;

  lists = signal<ReadingList[]>([]);
  listEntities = computed(() => this.lists().map(l => CardEntityFactory.readingList(l)));
  readingListConfig = computed(() => {
    return this.cardConfigFactory.forReadingList({shouldRenderAction: this.shouldRenderReadingListAction.bind(this)});
  });
  isLoadingLists = signal<boolean>(false);
  pagination!: Pagination;
  jumpbarKeys = signal<JumpKey[]>([]);
  actions: {[key: number]: Array<ActionItem<ReadingList>>} = {};
  globalActions: Array<ActionItem<any>> = []; // TODO: Why is this empty?
  trackByIdentity = (index: number, item: ReadingListCardEntity) => `${item.data.id}_${item.data.title}_${item.data.promoted}`;

  ngOnInit(): void {
    this.loadPage();

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

  getPage() {
    const urlParams = new URLSearchParams(window.location.search);
    return urlParams.get('page');
  }

  loadPage() {
    const page = this.getPage();
    if (page != null) {
      this.pagination.currentPage = parseInt(page, 10);
    }
    this.isLoadingLists.set(true);
    this.cdRef.markForCheck();

    this.readingListService.getReadingLists(true, false).subscribe((readingLists: PaginatedResult<ReadingList[]>) => {
      this.lists.set(readingLists.result);
      this.pagination = readingLists.pagination;
      this.jumpbarKeys.set(this.jumpbarService.getJumpKeys(readingLists.result, (rl: ReadingList) => rl.title));
      this.isLoadingLists.set(false);
      this.cdRef.markForCheck();
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
}
