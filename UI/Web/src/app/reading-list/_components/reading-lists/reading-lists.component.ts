import {ChangeDetectionStrategy, ChangeDetectorRef, Component, HostListener, inject, OnInit} from '@angular/core';
import { Router } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
import { JumpKey } from 'src/app/_models/jumpbar/jump-key';
import { PaginatedResult, Pagination } from 'src/app/_models/pagination';
import { ReadingList } from 'src/app/_models/reading-list';
import { AccountService } from 'src/app/_services/account.service';
import { Action, ActionFactoryService, ActionItem } from 'src/app/_services/action-factory.service';
import { ActionService } from 'src/app/_services/action.service';
import { ImageService } from 'src/app/_services/image.service';
import { JumpbarService } from 'src/app/_services/jumpbar.service';
import { ReadingListService } from 'src/app/_services/reading-list.service';
import { CardItemComponent } from '../../../cards/card-item/card-item.component';
import { CardDetailLayoutComponent } from '../../../cards/card-detail-layout/card-detail-layout.component';
import { NgIf, DecimalPipe } from '@angular/common';
import { SideNavCompanionBarComponent } from '../../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {CardActionablesComponent} from "../../../_single-module/card-actionables/card-actionables.component";
import {Title} from "@angular/platform-browser";
import {WikiLink} from "../../../_models/wiki";
import {BulkSelectionService} from "../../../cards/bulk-selection.service";
import {BulkOperationsComponent} from "../../../cards/bulk-operations/bulk-operations.component";
import {KEY_CODES} from "../../../shared/_services/utility.service";

@Component({
    selector: 'app-reading-lists',
    templateUrl: './reading-lists.component.html',
    styleUrls: ['./reading-lists.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [SideNavCompanionBarComponent, CardActionablesComponent, NgIf, CardDetailLayoutComponent, CardItemComponent, DecimalPipe, TranslocoDirective, BulkOperationsComponent]
})
export class ReadingListsComponent implements OnInit {

  public readonly bulkSelectionService = inject(BulkSelectionService);
  public readonly actionService = inject(ActionService);

  protected readonly WikiLink = WikiLink;

  lists: ReadingList[] = [];
  loadingLists = false;
  pagination!: Pagination;
  isAdmin: boolean = false;
  hasPromote: boolean = false;
  jumpbarKeys: Array<JumpKey> = [];
  actions: {[key: number]: Array<ActionItem<ReadingList>>} = {};
  globalActions: Array<ActionItem<any>> = [];
  trackByIdentity = (index: number, item: ReadingList) => `${item.id}_${item.title}_${item.promoted}`;


  constructor(private readingListService: ReadingListService, public imageService: ImageService, private actionFactoryService: ActionFactoryService,
    private accountService: AccountService, private toastr: ToastrService, private router: Router,
    private jumpbarService: JumpbarService, private readonly cdRef: ChangeDetectorRef, private ngbModal: NgbModal, private titleService: Title) { }

  ngOnInit(): void {
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.isAdmin = this.accountService.hasAdminRole(user);
        this.hasPromote = this.accountService.hasPromoteRole(user);

        this.cdRef.markForCheck();

        this.loadPage();
        this.titleService.setTitle('Kavita - ' + translate('side-nav.reading-lists'));
      }
    });
  }

  getActions(readingList: ReadingList) {
    const d = this.actionFactoryService.getReadingListActions(this.handleReadingListActionCallback.bind(this))
      .filter(action => this.readingListService.actionListFilter(action, readingList, this.isAdmin || this.hasPromote));

    return this.actionFactoryService.getReadingListActions(this.handleReadingListActionCallback.bind(this))
      .filter(action => this.readingListService.actionListFilter(action, readingList, this.isAdmin || this.hasPromote));
  }

  performGlobalAction(action: ActionItem<any>) {
    if (typeof action.callback === 'function') {
      action.callback(action, undefined);
    }
  }

  handleClick(list: ReadingList) {
    this.router.navigateByUrl('lists/' + list.id);
  }

  handleReadingListActionCallback(action: ActionItem<ReadingList>, readingList: ReadingList) {
    switch(action.action) {
      case Action.Delete:
        this.readingListService.delete(readingList.id).subscribe(() => {
          this.toastr.success(translate('toasts.reading-list-deleted'));
          this.loadPage();
        });
        break;
      case Action.Edit:
        this.actionService.editReadingList(readingList, (updatedList: ReadingList) => {
          // Reload information around list
          readingList = updatedList;
          this.cdRef.markForCheck();
        });
        break;
      case Action.Promote:
        this.actionService.promoteMultipleReadingLists([readingList], true, (res) => {
          // Reload information around list
          readingList.promoted = true;
          this.loadPage();
          this.cdRef.markForCheck();
        });
        break;
      case Action.UnPromote:
        this.actionService.promoteMultipleReadingLists([readingList], false, (res) => {
          // Reload information around list
          readingList.promoted = false;
          this.loadPage();
          this.cdRef.markForCheck();
        });
        break;
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

    this.readingListService.getReadingLists(true, false).pipe(take(1)).subscribe((readingLists: PaginatedResult<ReadingList[]>) => {
      this.lists = readingLists.result;
      this.pagination = readingLists.pagination;
      this.jumpbarKeys = this.jumpbarService.getJumpKeys(readingLists.result, (rl: ReadingList) => rl.title);
      this.loadingLists = false;
      this.actions = {};
      this.lists.forEach(l => this.actions[l.id] = this.getActions(l));
      this.cdRef.markForCheck();
    });
  }

  bulkActionCallback = (action: ActionItem<any>, data: any) => {
    const selectedReadingListIndexies = this.bulkSelectionService.getSelectedCardsForSource('readingList');
    const selectedReadingLists = this.lists.filter((col, index: number) => selectedReadingListIndexies.includes(index + ''));

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
}
