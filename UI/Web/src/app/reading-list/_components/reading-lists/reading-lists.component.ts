import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit} from '@angular/core';
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
import { ImportCblModalComponent } from '../../_modals/import-cbl-modal/import-cbl-modal.component';
import { CardItemComponent } from '../../../cards/card-item/card-item.component';
import { CardDetailLayoutComponent } from '../../../cards/card-detail-layout/card-detail-layout.component';
import { NgIf, DecimalPipe } from '@angular/common';
import { SideNavCompanionBarComponent } from '../../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';
import {TranslocoDirective, TranslocoService} from "@ngneat/transloco";
import {CardActionablesComponent} from "../../../_single-module/card-actionables/card-actionables.component";
import {CollectionTag} from "../../../_models/collection-tag";

@Component({
    selector: 'app-reading-lists',
    templateUrl: './reading-lists.component.html',
    styleUrls: ['./reading-lists.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [SideNavCompanionBarComponent, CardActionablesComponent, NgIf, CardDetailLayoutComponent, CardItemComponent, DecimalPipe, TranslocoDirective]
})
export class ReadingListsComponent implements OnInit {

  lists: ReadingList[] = [];
  loadingLists = false;
  pagination!: Pagination;
  isAdmin: boolean = false;
  jumpbarKeys: Array<JumpKey> = [];
  actions: {[key: number]: Array<ActionItem<ReadingList>>} = {};
  globalActions: Array<ActionItem<any>> = [{action: Action.Import, title: 'import-cbl', children: [], requiresAdmin: true, callback: this.importCbl.bind(this)}];
  trackByIdentity = (index: number, item: ReadingList) => `${item.id}_${item.title}`;

  translocoService = inject(TranslocoService);
  constructor(private readingListService: ReadingListService, public imageService: ImageService, private actionFactoryService: ActionFactoryService,
    private accountService: AccountService, private toastr: ToastrService, private router: Router, private actionService: ActionService,
    private jumpbarService: JumpbarService, private readonly cdRef: ChangeDetectorRef, private ngbModal: NgbModal) { }

  ngOnInit(): void {
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.isAdmin = this.accountService.hasAdminRole(user);
        this.loadPage();
      }
    });
  }

  getActions(readingList: ReadingList) {
    return this.actionFactoryService.getReadingListActions(this.handleReadingListActionCallback.bind(this))
      .filter(action => this.readingListService.actionListFilter(action, readingList, this.isAdmin));
  }

  performAction(action: ActionItem<ReadingList>, readingList: ReadingList) {
    if (typeof action.callback === 'function') {
      action.callback(action, readingList);
    }
  }

  performGlobalAction(action: ActionItem<any>) {
    if (typeof action.callback === 'function') {
      action.callback(action, undefined);
    }
  }

  importCbl() {
    const ref = this.ngbModal.open(ImportCblModalComponent, {size: 'xl'});
    ref.closed.subscribe(result => this.loadPage());
    ref.dismissed.subscribe(_ => this.loadPage());
  }

  handleReadingListActionCallback(action: ActionItem<ReadingList>, readingList: ReadingList) {
    switch(action.action) {
      case Action.Delete:
        this.readingListService.delete(readingList.id).subscribe(() => {
          this.toastr.success(this.translocoService.translate('toasts.reading-list-deleted'));
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
      window.scrollTo(0, 0);
      this.cdRef.markForCheck();
    });
  }

  handleClick(list: ReadingList) {
    this.router.navigateByUrl('lists/' + list.id);
  }
}
