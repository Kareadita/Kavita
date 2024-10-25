import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit} from '@angular/core';
import {ActivatedRoute, Router} from '@angular/router';
import {ToastrService} from 'ngx-toastr';
import {take} from 'rxjs/operators';
import {ConfirmService} from 'src/app/shared/confirm.service';
import {Breakpoint, UtilityService} from 'src/app/shared/_services/utility.service';
import {LibraryType} from 'src/app/_models/library/library';
import {MangaFormat} from 'src/app/_models/manga-format';
import {ReadingList, ReadingListItem} from 'src/app/_models/reading-list';
import {AccountService} from 'src/app/_services/account.service';
import {Action, ActionFactoryService, ActionItem} from 'src/app/_services/action-factory.service';
import {ActionService} from 'src/app/_services/action.service';
import {ImageService} from 'src/app/_services/image.service';
import {ReadingListService} from 'src/app/_services/reading-list.service';
import {
  DraggableOrderedListComponent,
  IndexUpdateEvent
} from '../draggable-ordered-list/draggable-ordered-list.component';
import {forkJoin, Observable} from 'rxjs';
import {ReaderService} from 'src/app/_services/reader.service';
import {LibraryService} from 'src/app/_services/library.service';
import {Person} from 'src/app/_models/metadata/person';
import {ReadingListItemComponent} from '../reading-list-item/reading-list-item.component';
import {LoadingComponent} from '../../../shared/loading/loading.component';
import {A11yClickDirective} from '../../../shared/a11y-click.directive';
import {PersonBadgeComponent} from '../../../shared/person-badge/person-badge.component';
import {BadgeExpanderComponent} from '../../../shared/badge-expander/badge-expander.component';
import {ReadMoreComponent} from '../../../shared/read-more/read-more.component';
import {NgbDropdown, NgbDropdownItem, NgbDropdownMenu, NgbDropdownToggle} from '@ng-bootstrap/ng-bootstrap';
import {ImageComponent} from '../../../shared/image/image.component';
import {AsyncPipe, DatePipe, DecimalPipe, NgClass} from '@angular/common';
import {
  SideNavCompanionBarComponent
} from '../../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {CardActionablesComponent} from "../../../_single-module/card-actionables/card-actionables.component";
import {FilterUtilitiesService} from "../../../shared/_services/filter-utilities.service";
import {FilterField} from "../../../_models/metadata/v2/filter-field";
import {FilterComparison} from "../../../_models/metadata/v2/filter-comparison";
import {MetadataDetailComponent} from "../../../series-detail/_components/metadata-detail/metadata-detail.component";
import {Title} from "@angular/platform-browser";

@Component({
    selector: 'app-reading-list-detail',
    templateUrl: './reading-list-detail.component.html',
    styleUrls: ['./reading-list-detail.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [SideNavCompanionBarComponent, CardActionablesComponent, ImageComponent, NgbDropdown,
    NgbDropdownToggle, NgbDropdownMenu, NgbDropdownItem, ReadMoreComponent, BadgeExpanderComponent,
    PersonBadgeComponent, A11yClickDirective, LoadingComponent, DraggableOrderedListComponent,
    ReadingListItemComponent, NgClass, AsyncPipe, DecimalPipe, DatePipe, TranslocoDirective,
    MetadataDetailComponent]
})
export class ReadingListDetailComponent implements OnInit {

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private readingListService = inject(ReadingListService);
  private actionService = inject(ActionService);
  private actionFactoryService = inject(ActionFactoryService);
  public utilityService = inject(UtilityService);
  public imageService = inject(ImageService);
  private accountService = inject(AccountService);
  private toastr = inject(ToastrService);
  private confirmService = inject(ConfirmService);
  private libraryService = inject(LibraryService);
  private readerService = inject(ReaderService);
  private cdRef = inject(ChangeDetectorRef);
  private filterUtilityService = inject(FilterUtilitiesService);
  private titleService = inject(Title);

  protected readonly MangaFormat = MangaFormat;
  protected readonly Breakpoint = Breakpoint;

  items: Array<ReadingListItem> = [];
  listId!: number;
  readingList: ReadingList | undefined;
  actions: Array<ActionItem<any>> = [];
  isAdmin: boolean = false;
  isLoading: boolean = false;
  accessibilityMode: boolean = false;
  readingListSummary: string = '';

  libraryTypes: {[key: number]: LibraryType} = {};
  characters$!: Observable<Person[]>;




  ngOnInit(): void {
    const listId = this.route.snapshot.paramMap.get('id');

    if (listId === null) {
      this.router.navigateByUrl('/home');
      return;
    }

    this.listId = parseInt(listId, 10);
    this.characters$ = this.readingListService.getCharacters(this.listId);

    this.accessibilityMode = this.utilityService.getActiveBreakpoint() < Breakpoint.Tablet;
    this.cdRef.markForCheck();

    forkJoin([
      this.libraryService.getLibraries(),
      this.readingListService.getReadingList(this.listId)
    ]).subscribe(results => {
      const libraries = results[0];
      const readingList = results[1];

      this.titleService.setTitle('Kavita - ' + readingList.title);

      libraries.forEach(lib => {
        this.libraryTypes[lib.id] = lib.type;
      });

      if (readingList == null) {
        // The list doesn't exist
        this.toastr.error(translate('toasts.list-doesnt-exist'));
        this.router.navigateByUrl('library');
        return;
      }
      this.readingList = readingList;
      this.readingListSummary = (this.readingList.summary === null ? '' : this.readingList.summary).replace(/\n/g, '<br>');

      this.cdRef.markForCheck();

      this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
        if (user) {
          this.isAdmin = this.accountService.hasAdminRole(user);

          this.actions = this.actionFactoryService.getReadingListActions(this.handleReadingListActionCallback.bind(this))
            .filter(action => this.readingListService.actionListFilter(action, readingList, this.isAdmin));
          this.cdRef.markForCheck();
        }
      });
    });
    this.getListItems();
  }

  getListItems() {
    this.isLoading = true;
    this.cdRef.markForCheck();

    this.readingListService.getListItems(this.listId).subscribe(items => {
      this.items = [...items];
      this.isLoading = false;
      this.cdRef.markForCheck();
    });
  }

  performAction(action: ActionItem<any>) {
    if (typeof action.callback === 'function') {
      action.callback(action, this.readingList);
    }
  }

  readChapter(item: ReadingListItem) {
    if (!this.readingList) return;
    const params = this.readerService.getQueryParamsObject(false, true, this.readingList.id);
    this.router.navigate(this.readerService.getNavigationArray(item.libraryId, item.seriesId, item.chapterId, item.seriesFormat), {queryParams: params});
  }

  async handleReadingListActionCallback(action: ActionItem<ReadingList>, readingList: ReadingList) {
    switch(action.action) {
      case Action.Delete:
        await this.deleteList(readingList);
        break;
      case Action.Edit:
        this.actionService.editReadingList(readingList, (readingList: ReadingList) => {
          // Reload information around list
          this.readingListService.getReadingList(this.listId).subscribe(rl => {
            this.readingList = rl;
            this.readingListSummary = (this.readingList.summary === null ? '' : this.readingList.summary).replace(/\n/g, '<br>');
            this.cdRef.markForCheck();
          });
        });
        break;
      case Action.Promote:
        this.actionService.promoteMultipleReadingLists([this.readingList!], true, () => {
          if (this.readingList) {
            this.readingList.promoted = true;
            this.cdRef.markForCheck();
          }
        });
        break;
      case Action.UnPromote:
        this.actionService.promoteMultipleReadingLists([this.readingList!], false, () => {
          if (this.readingList) {
            this.readingList.promoted = false;
            this.cdRef.markForCheck();
          }
        });
        break;
    }
  }

  async deleteList(readingList: ReadingList) {
    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-reading-list'))) return;

    this.readingListService.delete(readingList.id).subscribe(() => {
      this.toastr.success(translate('toasts.reading-list-deleted'));
      this.router.navigateByUrl('/lists');
    });
  }

  orderUpdated(event: IndexUpdateEvent) {
    if (!this.readingList) return;
    this.readingListService.updatePosition(this.readingList.id, event.item.id, event.fromPosition, event.toPosition).subscribe(() => {
      this.getListItems();
    });
  }

  itemRemoved(item: ReadingListItem, position: number) {
    if (!this.readingList) return;
    this.readingListService.deleteItem(this.readingList.id, item.id).subscribe(() => {
      this.items.splice(position, 1);
      this.items = [...this.items];
      this.cdRef.markForCheck();
      this.toastr.success(translate('toasts.item-removed'));
    });
  }

  removeRead() {
    if (!this.readingList) return;
    this.isLoading = true;
    this.cdRef.markForCheck();
    this.readingListService.removeRead(this.readingList.id).subscribe((resp) => {
      if (resp === 'Nothing to remove') {
        this.toastr.info(translate('toasts.nothing-to-remove'));
        return;
      }
      this.getListItems();
    });
  }

  read(incognitoMode: boolean = false) {
    if (!this.readingList) return;
    const firstItem = this.items[0];
    this.router.navigate(
      this.readerService.getNavigationArray(firstItem.libraryId, firstItem.seriesId, firstItem.chapterId, firstItem.seriesFormat),
      {queryParams: {readingListId: this.readingList.id, incognitoMode: incognitoMode}});
  }

  continue(incognitoMode: boolean = false) {
    // TODO: Can I do this in the backend?
    if (!this.readingList) return;
    let currentlyReadingChapter = this.items[0];
    for (let i = 0; i < this.items.length; i++) {
      if (this.items[i].pagesRead >= this.items[i].pagesTotal) {
        continue;
      }
      currentlyReadingChapter = this.items[i];
      break;
    }

    this.router.navigate(
      this.readerService.getNavigationArray(currentlyReadingChapter.libraryId, currentlyReadingChapter.seriesId, currentlyReadingChapter.chapterId, currentlyReadingChapter.seriesFormat),
      {queryParams: {readingListId: this.readingList.id, incognitoMode: incognitoMode}});
  }

  updateAccessibilityMode() {
    this.accessibilityMode = !this.accessibilityMode;
    this.cdRef.markForCheck();
  }

  goToCharacter(character: Person) {
    this.filterUtilityService.applyFilter(['all-series'], FilterField.Characters, FilterComparison.Contains, character.id + '').subscribe();
  }
}
