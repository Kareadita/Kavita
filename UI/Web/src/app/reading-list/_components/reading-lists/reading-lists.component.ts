import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  inject,
  OnInit,
  signal,
  TemplateRef,
  viewChild
} from '@angular/core';
import {ToastrService} from 'ngx-toastr';
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
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {CardActionablesComponent} from "../../../_single-module/card-actionables/card-actionables.component";
import {Title} from "@angular/platform-browser";
import {WikiLink} from "../../../_models/wiki";
import {BulkSelectionService} from "../../../cards/bulk-selection.service";
import {BulkOperationsComponent} from "../../../cards/bulk-operations/bulk-operations.component";
import {User} from "../../../_models/user/user";
import {EntityCardComponent} from "../../../cards/entity-card/entity-card.component";
import {CardEntity, CardEntityFactory, ReadingListCardEntity} from "../../../_models/card/card-entity";
import {CardConfigFactory} from "../../../_services/card-config-factory.service";
import {ActionItem} from "../../../_models/actionables/action-item";
import {Action} from "../../../_models/actionables/action";
import {ActionResult} from "../../../_models/actionables/action-result";
import {PromotedIconComponent} from "../../../shared/_components/promoted-icon/promoted-icon.component";

@Component({
  selector: 'app-reading-lists',
  templateUrl: './reading-lists.component.html',
  styleUrls: ['./reading-lists.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SideNavCompanionBarComponent, CardActionablesComponent, CardDetailLayoutComponent, DecimalPipe,
    TranslocoDirective, BulkOperationsComponent, EntityCardComponent, PromotedIconComponent]
})
export class ReadingListsComponent implements OnInit {
  private readingListService = inject(ReadingListService);
  private readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);
  private readonly jumpbarService = inject(JumpbarService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly titleService = inject(Title);
  private readonly cardConfigFactory = inject(CardConfigFactory);
  protected readonly bulkSelectionService = inject(BulkSelectionService);
  protected readonly actionService = inject(ActionService);

  protected readonly WikiLink = WikiLink;


  protected titleTemplateRef = viewChild<TemplateRef<{ $implicit: CardEntity }>>('title');


  lists = signal<ReadingList[]>([]);
  listEntities = computed(() => this.lists().map(l => CardEntityFactory.readingList(l)));
  readingListConfig = computed(() => {
    return this.cardConfigFactory.forReadingList(this.titleTemplateRef(), this.shouldRenderReadingListAction.bind(this));
  });
  loadingLists = false;
  pagination!: Pagination;
  jumpbarKeys: Array<JumpKey> = [];
  actions: {[key: number]: Array<ActionItem<ReadingList>>} = {};
  globalActions: Array<ActionItem<any>> = []; // TODO: Why is this empty? 
  trackByIdentity = (index: number, item: ReadingListCardEntity) => `${item.data.id}_${item.data.title}_${item.data.promoted}`;

  ngOnInit(): void {
    this.titleService.setTitle('Kavita - ' + translate('side-nav.reading-lists'));
    this.loadPage();
  }

  performGlobalAction(event: ActionItem<void> | ActionResult<void>) {
    // Skip ActionResults — they've already been handled
    if ('effect' in event) return;

    if (typeof event.callback === 'function') {
      event.callback(event, undefined)
    }
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
    this.loadingLists = true;
    this.cdRef.markForCheck();

    this.readingListService.getReadingLists(true, false).subscribe((readingLists: PaginatedResult<ReadingList[]>) => {
      this.lists.set(readingLists.result);
      this.pagination = readingLists.pagination;
      this.jumpbarKeys = this.jumpbarService.getJumpKeys(readingLists.result, (rl: ReadingList) => rl.title);
      this.loadingLists = false;
      this.cdRef.markForCheck();
    });
  }

  bulkActionCallback = (action: ActionItem<any>, data: any) => {
    const selectedReadingListIndexies = this.bulkSelectionService.getSelectedCardsForSource('readingList');
    const selectedReadingLists = this.lists().filter((col, index: number) => selectedReadingListIndexies.includes(index + ''));

    switch (action.action) {
      case Action.Promote:
        this.actionService.promoteMultipleReadingLists(selectedReadingLists, true, (success) => {
          if (!success) return;
          this.bulkSelectionService.deselectAll();
          this.loadPage();
        });
        break;
      case Action.UnPromote:
        this.actionService.promoteMultipleReadingLists(selectedReadingLists, false, (success) => {
          if (!success) return;
          this.bulkSelectionService.deselectAll();
          this.loadPage();
        });
        break;
      case Action.Delete:
        this.actionService.deleteMultipleReadingLists(selectedReadingLists, (successful) => {
          if (!successful) return;
          this.loadPage();
          this.bulkSelectionService.deselectAll();
        });
        break;
    }
  }

  shouldRenderReadingListAction(action: ActionItem<ReadingList>, entity: ReadingList, user: User) {
    const isPromoteAction = action.action === Action.Promote || action.action === Action.UnPromote;
    const hasPromotionAbility = this.accountService.hasAdminRole(user) || this.accountService.hasPromoteRole(user);

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
