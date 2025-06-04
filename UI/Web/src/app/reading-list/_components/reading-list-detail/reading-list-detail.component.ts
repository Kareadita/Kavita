import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  ElementRef,
  Inject,
  inject,
  OnInit,
  ViewChild
} from '@angular/core';
import {ActivatedRoute, Router, RouterLink} from '@angular/router';
import {AsyncPipe, DatePipe, DecimalPipe, DOCUMENT, Location, NgClass, NgStyle} from '@angular/common';
import {ToastrService} from 'ngx-toastr';
import {take} from 'rxjs/operators';
import {ConfirmService} from 'src/app/shared/confirm.service';
import {Breakpoint, UtilityService} from 'src/app/shared/_services/utility.service';
import {LibraryType} from 'src/app/_models/library/library';
import {MangaFormat} from 'src/app/_models/manga-format';
import {ReadingList, ReadingListInfo, ReadingListItem} from 'src/app/_models/reading-list';
import {AccountService} from 'src/app/_services/account.service';
import {Action, ActionFactoryService, ActionItem} from 'src/app/_services/action-factory.service';
import {ActionService} from 'src/app/_services/action.service';
import {ImageService} from 'src/app/_services/image.service';
import {ReadingListService} from 'src/app/_services/reading-list.service';
import {
  DraggableOrderedListComponent,
  IndexUpdateEvent
} from '../draggable-ordered-list/draggable-ordered-list.component';
import {forkJoin, startWith, tap} from 'rxjs';
import {ReaderService} from 'src/app/_services/reader.service';
import {LibraryService} from 'src/app/_services/library.service';
import {ReadingListItemComponent} from '../reading-list-item/reading-list-item.component';
import {LoadingComponent} from '../../../shared/loading/loading.component';
import {BadgeExpanderComponent} from '../../../shared/badge-expander/badge-expander.component';
import {ReadMoreComponent} from '../../../shared/read-more/read-more.component';
import {
  NgbDropdown,
  NgbDropdownItem,
  NgbDropdownMenu,
  NgbDropdownToggle,
  NgbNav,
  NgbNavChangeEvent,
  NgbNavContent,
  NgbNavItem,
  NgbNavLink,
  NgbNavOutlet,
  NgbTooltip
} from '@ng-bootstrap/ng-bootstrap';
import {ImageComponent} from '../../../shared/image/image.component';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {CardActionablesComponent} from "../../../_single-module/card-actionables/card-actionables.component";
import {Title} from "@angular/platform-browser";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {VirtualScrollerModule} from "@iharbeck/ngx-virtual-scroller";
import {PromotedIconComponent} from "../../../shared/_components/promoted-icon/promoted-icon.component";
import {DefaultValuePipe} from "../../../_pipes/default-value.pipe";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {DetailsTabComponent} from "../../../_single-module/details-tab/details-tab.component";
import {IHasCast} from "../../../_models/common/i-has-cast";
import {User} from "../../../_models/user";

enum TabID {
  Storyline = 'storyline-tab',
  Volumes = 'volume-tab',
  Details = 'details-tab',
}

@Component({
  selector: 'app-reading-list-detail',
  templateUrl: './reading-list-detail.component.html',
  styleUrls: ['./reading-list-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CardActionablesComponent, ImageComponent, NgbDropdown,
    NgbDropdownToggle, NgbDropdownMenu, NgbDropdownItem, ReadMoreComponent, BadgeExpanderComponent,
    LoadingComponent, DraggableOrderedListComponent,
    ReadingListItemComponent, NgClass, AsyncPipe, DecimalPipe, DatePipe, TranslocoDirective, ReactiveFormsModule,
    NgbNav, NgbNavContent, NgbNavLink, NgbTooltip,
    RouterLink, VirtualScrollerModule, NgStyle, NgbNavOutlet, NgbNavItem, PromotedIconComponent, DefaultValuePipe, DetailsTabComponent]
})
export class ReadingListDetailComponent implements OnInit {

  protected readonly MangaFormat = MangaFormat;
  protected readonly Breakpoint = Breakpoint;
  protected readonly TabID = TabID;
  protected readonly encodeURIComponent = encodeURIComponent;

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private readingListService = inject(ReadingListService);
  private actionService = inject(ActionService);
  private actionFactoryService = inject(ActionFactoryService);
  protected utilityService = inject(UtilityService);
  protected imageService = inject(ImageService);
  private accountService = inject(AccountService);
  private toastr = inject(ToastrService);
  private confirmService = inject(ConfirmService);
  private libraryService = inject(LibraryService);
  private readerService = inject(ReaderService);
  private cdRef = inject(ChangeDetectorRef);
  private titleService = inject(Title);
  private location = inject(Location);
  private destroyRef = inject(DestroyRef);



  @ViewChild('scrollingBlock') scrollingBlock: ElementRef<HTMLDivElement> | undefined;
  @ViewChild('companionBar') companionBar: ElementRef<HTMLDivElement> | undefined;

  items: Array<ReadingListItem> = [];
  listId!: number;
  readingList: ReadingList | undefined;
  actions: Array<ActionItem<any>> = [];
  isAdmin: boolean = false;
  isLoading: boolean = false;
  accessibilityMode: boolean = false;
  editMode: boolean = false;
  readingListSummary: string = '';

  libraryTypes: {[key: number]: LibraryType} = {};
  activeTabId = TabID.Storyline;
  showStorylineTab = true;
  isOwnedReadingList: boolean = false;
  rlInfo: ReadingListInfo | null = null;
  castInfo: IHasCast = {
    characterLocked: false,
    characters: [],
    coloristLocked: false,
    colorists: [],
    coverArtistLocked: false,
    coverArtists: [],
    editorLocked: false,
    editors: [],
    imprintLocked: false,
    imprints: [],
    inkerLocked: false,
    inkers: [],
    languageLocked: false,
    lettererLocked: false,
    letterers: [],
    locationLocked: false,
    locations: [],
    pencillerLocked: false,
    pencillers: [],
    publisherLocked: false,
    publishers: [],
    teamLocked: false,
    teams: [],
    translatorLocked: false,
    translators: [],
    writerLocked: false,
    writers: []
  };

  formGroup = new FormGroup({
    'edit': new FormControl(false, []),
    'accessibilityMode': new FormControl(false, []),
  });


  get ScrollingBlockHeight() {
    if (this.scrollingBlock === undefined) return 'calc(var(--vh)*100)';
    const navbar = this.document.querySelector('.navbar') as HTMLElement;
    if (navbar === null) return 'calc(var(--vh)*100)';

    const companionHeight = this.companionBar?.nativeElement.offsetHeight || 0;
    const navbarHeight = navbar.offsetHeight;
    const totalHeight = companionHeight + navbarHeight + 21; //21px to account for padding
    return 'calc(var(--vh)*100 - ' + totalHeight + 'px)';
  }

  constructor(@Inject(DOCUMENT) private document: Document) {}


  ngOnInit(): void {
    const listId = this.route.snapshot.paramMap.get('id');

    if (listId === null) {
      this.router.navigateByUrl('/home');
      return;
    }

    this.listId = parseInt(listId, 10);


    this.readingListService.getAllPeople(this.listId).subscribe(allPeople => {
      this.castInfo = allPeople;
      this.cdRef.markForCheck();
    });




    this.readingListService.getReadingListInfo(this.listId).subscribe(info => {
      this.rlInfo = info;
      this.cdRef.markForCheck();
    });

    this.formGroup.get('edit')!.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef),
      startWith(false),
      tap(mode => {
        this.editMode = (mode || false);
        this.cdRef.markForCheck();
      })
    ).subscribe();

    this.formGroup.get('accessibilityMode')!.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef),
      startWith(this.utilityService.getActiveBreakpoint() < Breakpoint.Tablet),
      tap(mode => {
        this.accessibilityMode = (mode || this.utilityService.getActiveBreakpoint() < Breakpoint.Tablet);
        this.cdRef.markForCheck();
      })
    ).subscribe();

    if (this.utilityService.getActiveBreakpoint() < Breakpoint.Tablet) {
      this.formGroup.get('accessibilityMode')?.disable();
    }

    this.accessibilityMode = this.utilityService.getActiveBreakpoint() < Breakpoint.Tablet;
    this.editMode = false;
    this.cdRef.markForCheck();

    forkJoin([
      this.libraryService.getLibraries(),
      this.readingListService.getReadingList(this.listId)
    ]).subscribe(results => {
      const libraries = results[0];
      const readingList = results[1];



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
      this.titleService.setTitle('Kavita - ' + readingList.title);

      this.cdRef.markForCheck();

      this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
        if (user) {
          this.isAdmin = this.accountService.hasAdminRole(user);

          this.actions = this.actionFactoryService
            .getReadingListActions(this.handleReadingListActionCallback.bind(this), this.shouldRenderReadingListAction.bind(this))
            .filter(action => this.readingListService.actionListFilter(action, readingList, this.isAdmin));
          this.isOwnedReadingList = this.actions.filter(a => a.action === Action.Edit).length > 0;
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
        this.editReadingList(readingList);
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

  shouldRenderReadingListAction(action: ActionItem<ReadingList>, entity: ReadingList, user: User) {
    switch (action.action) {
      case Action.Promote:
        return !entity.promoted;
      case Action.UnPromote:
          return entity.promoted;
      default:
        return true;
    }
  }

  editReadingList(readingList: ReadingList) {
    this.actionService.editReadingList(readingList, (readingList: ReadingList) => {
      // Reload information around list
      this.readingListService.getReadingList(this.listId).subscribe(rl => {
        this.readingList = rl!;
        this.readingListSummary = (this.readingList.summary === null ? '' : this.readingList.summary).replace(/\n/g, '<br>');
        this.cdRef.markForCheck();
      });
    });
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

  toggleReorder() {
    this.formGroup.get('edit')?.setValue(!this.formGroup.get('edit')!.value);
    this.cdRef.markForCheck();
  }


  onNavChange(event: NgbNavChangeEvent) {
    this.updateUrl(event.nextId);
    this.cdRef.markForCheck();
  }

  private updateUrl(activeTab: TabID) {
    const tokens = this.location.path().split('#');
    const newUrl = `${tokens[0]}#${activeTab}`;
    this.location.replaceState(newUrl)
  }

  switchTabsToDetail() {
    this.activeTabId = TabID.Details;
    this.cdRef.markForCheck();
    setTimeout(() => {
      const tabElem = this.document.querySelector('#details-tab');
      if (tabElem) {
        (tabElem as HTMLLIElement).scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'center' });
      }
    }, 10);
  }
}
