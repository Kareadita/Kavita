import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  input,
  numberAttribute,
  OnInit,
  signal,
  TrackByFunction,
  viewChild
} from '@angular/core';
import {ActivatedRoute, Router, RouterLink} from '@angular/router';
import {DecimalPipe, DOCUMENT, Location, NgClass, NgStyle} from '@angular/common';
import {ToastrService} from 'ngx-toastr';
import {UtilityService} from 'src/app/shared/_services/utility.service';
import {LibraryType} from 'src/app/_models/library/library';
import {MangaFormat} from 'src/app/_models/manga-format';
import {ReadingList, ReadingListInfo, ReadingListItem} from 'src/app/_models/reading-list/reading-list';
import {AccountService} from 'src/app/_services/account.service';
import {ActionFactoryService} from 'src/app/_services/action-factory.service';
import {ActionService} from 'src/app/_services/action.service';
import {ImageService} from 'src/app/_services/image.service';
import {ReadingListService} from 'src/app/_services/reading-list.service';
import {
  DraggableOrderedListComponent,
  IndexUpdateEvent,
  ItemRemoveEvent
} from '../draggable-ordered-list/draggable-ordered-list.component';
import {startWith, tap} from 'rxjs';
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
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {VirtualScrollerModule} from "@iharbeck/ngx-virtual-scroller";
import {PromotedIconComponent} from "../../../shared/_components/promoted-icon/promoted-icon.component";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {DetailsTabComponent} from "../../../_single-module/details-tab/details-tab.component";
import {IHasCast} from "../../../_models/common/i-has-cast";
import {User} from "../../../_models/user/user";
import {Breakpoint, BreakpointService} from "../../../_services/breakpoint.service";
import {ActionItem} from "../../../_models/actionables/action-item";
import {Action} from "../../../_models/actionables/action";
import {ActionResult} from "../../../_models/actionables/action-result";
import {getWritableResolvedData} from "../../../../libs/route-util";
import {Tabs} from "../../../_models/tabs";
import {TabTitlePipe} from "../../../_pipes/tab-title.pipe";
import {ConfirmService} from "../../../shared/confirm.service";
import {ColorscapeService} from "../../../_services/colorscape.service";
import {DateYearRangePipe} from "../../../_pipes/date-year-range.pipe";
import {FilterField} from "../../../_models/metadata/v2/filter-field";
import {FilterComparison} from "../../../_models/metadata/v2/filter-comparison";
import {FilterUtilitiesService} from "../../../shared/_services/filter-utilities.service";


@Component({
  selector: 'app-reading-list-detail',
  templateUrl: './reading-list-detail.component.html',
  styleUrls: ['./reading-list-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CardActionablesComponent, ImageComponent, NgbDropdown,
    NgbDropdownToggle, NgbDropdownMenu, NgbDropdownItem, ReadMoreComponent, BadgeExpanderComponent,
    LoadingComponent, DraggableOrderedListComponent,
    ReadingListItemComponent, NgClass, DecimalPipe, TranslocoDirective, ReactiveFormsModule,
    NgbNav, NgbNavContent, NgbNavLink, NgbTooltip,
    RouterLink, VirtualScrollerModule, NgStyle, NgbNavOutlet, NgbNavItem,
    PromotedIconComponent, DetailsTabComponent, TabTitlePipe]
})
export class ReadingListDetailComponent implements OnInit {
  private readonly document = inject<Document>(DOCUMENT);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly readingListService = inject(ReadingListService);
  private readonly actionService = inject(ActionService);
  private readonly actionFactoryService = inject(ActionFactoryService);
  protected readonly utilityService = inject(UtilityService);
  protected readonly imageService = inject(ImageService);
  private readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);
  private readonly libraryService = inject(LibraryService);
  private readonly readerService = inject(ReaderService);
  private readonly location = inject(Location);
  private readonly destroyRef = inject(DestroyRef);
  private readonly confirmService = inject(ConfirmService);
  protected readonly breakpointService = inject(BreakpointService);
  private readonly colorscapeService = inject(ColorscapeService);
  private readonly filterUtilityService = inject(FilterUtilitiesService);

  protected readonly MangaFormat = MangaFormat;
  protected readonly Tabs = Tabs;
  protected readonly encodeURIComponent = encodeURIComponent;
  private readonly dateYearRangePipe = new DateYearRangePipe();

  scrollingBlock = viewChild<ElementRef<HTMLDivElement>>('scrollingBlock');
  readingListId = input(0, {transform: numberAttribute});
  readingList = getWritableResolvedData(this.route, 'readingList');


  readingListSummary = computed(() => {
    return (this.readingList()?.summary || '').replace(/\n/g, '<br>');
  });
  imageUrl = computed(() => {
    const rl = this.readingList();
    return rl ? this.imageService.getReadingListCoverImage(rl.id) : '';
  });
  dateRangeLabel = computed<string | null>(() => {
    const rl = this.readingList();
    if (!rl || rl.startingYear === 0) return null;

    // Reading list dates start with 1, JS Date starts with 0
    const startMonth = rl.startingMonth > 0 ? rl.startingMonth - 1 : undefined;
    const endMonth = rl.startingMonth > 0 ? rl.endingMonth - 1 : undefined;

    const startDate = startMonth !== undefined ? new Date(rl.startingYear, startMonth) : new Date(rl.startingYear);
    const endDate = rl.endingYear <= 0 ? null :
      (endMonth !== undefined ? new Date(rl.endingYear, endMonth) : new Date(rl.endingYear));

    return this.dateYearRangePipe.transform(startDate, endDate, !!endMonth);
  });

  items = signal<Array<ReadingListItem>>([]);
  actions = computed(() => this.actionFactoryService
    .getReadingListActions(this.shouldRenderReadingListAction.bind(this))
    .filter(action => this.readingListService.actionListFilter(action, this.readingList(), this.isAdmin())));
  isAdmin = this.accountService.hasAdminRole;
  isLoading = signal(false);
  accessibilityMode = signal(false);
  editMode = signal(false);
  missingItems = computed(() => {
    const rl = this.readingList();
    if (rl.totalItemsAtImport === 0) return 0;
    if (rl.itemCount > rl.totalItemsAtImport) return 0;
    return rl.totalItemsAtImport - rl.itemCount;
  });

  libraryTypes = signal<{[key: number]: LibraryType}>({});
  activeTabId = Tabs.Storyline;
  isOwnedReadingList = computed(() => this.actions().filter(a => a.action === Action.Edit).length > 0);
  rlInfo = signal<ReadingListInfo | null>(null);
  castInfo = signal<IHasCast>({
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
  });

  filterText = signal('');
  filterFn = computed<((item: ReadingListItem) => boolean) | null>(() => {
    const text = this.filterText();
    if (!text) return null;
    return (item: ReadingListItem) => {
      return !!(item.title?.toLowerCase().includes(text)
        || item.seriesName?.toLowerCase().includes(text)
        || item.chapterNumber?.toLowerCase().includes(text)
        || item.volumeNumber?.toLowerCase().includes(text)
        || item.chapter?.titleName?.toLowerCase().includes(text)
        || item.chapter?.writerName?.toLowerCase().includes(text)
        || item.chapter?.pencillerName?.toLowerCase().includes(text));
    };
  });

  formGroup = new FormGroup({
    'edit': new FormControl(false, []),
    'accessibilityMode': new FormControl(false, []),
    'filter': new FormControl('', []),
  });

  trackByIdentity: TrackByFunction<ReadingListItem> = (index, item) => `${item.order}_${item.title}_${item.summary?.length}_${item.pagesRead}_${item.chapterId}`;

  get ScrollingBlockHeight() {
    if (this.scrollingBlock() === undefined) return 'calc(var(--vh)*100)';
    const navbar = this.document.querySelector('.navbar') as HTMLElement;
    if (navbar === null) return 'calc(var(--vh)*100)';

    const navbarHeight = navbar.offsetHeight;
    const totalHeight = navbarHeight + 21; //21px to account for padding
    return 'calc(var(--vh)*100 - ' + totalHeight + 'px)';
  }

  constructor() {
    // Form subscriptions
    this.formGroup.get('edit')!.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef),
      startWith(false),
      tap(mode => {
        this.editMode.set(mode || false);
      })
    ).subscribe();

    this.formGroup.get('accessibilityMode')!.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef),
      startWith(this.breakpointService.isMobile()),
      tap(mode => {
        this.accessibilityMode.set(mode || this.breakpointService.isMobile());
      })
    ).subscribe();

    this.formGroup.get('filter')!.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef),
      tap(val => this.filterText.set((val || '').trim().toLowerCase()))
    ).subscribe();

    if (this.breakpointService.isMobile()) {
      this.formGroup.get('accessibilityMode')?.disable();
    }

    this.accessibilityMode.set(this.breakpointService.isMobile());

    // Fetch libraries
    this.libraryService.getLibraries().pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(libraries => {
      const types: {[key: number]: LibraryType} = {};
      libraries.forEach(lib => {
        types[lib.id] = lib.type;
      });
      this.libraryTypes.set(types);
    });
  }

  ngOnInit() {
    const id = this.readingListId();

    if (this.readingList().coverImage) {
      this.colorscapeService.setColorScape(this.readingList().primaryColor, this.readingList().secondaryColor);
    }

    this.readingListService.getAllPeople(id).subscribe(allPeople => {
      this.castInfo.set(allPeople);
    });

    this.readingListService.getReadingListInfo(id).subscribe(info => {
      this.rlInfo.set(info);
    });

    this.getListItems();
  }

  getListItems() {
    this.isLoading.set(true);

    this.readingListService.getListItems(this.readingListId()).subscribe(items => {
      this.items.set([...items]);
      this.isLoading.set(false);
    });
  }


  readChapter(item: ReadingListItem) {
    const currentList = this.readingList();
    if (!currentList) return;
    const params = this.readerService.getQueryParamsObject(false, true, currentList.id);
    this.router.navigate(this.readerService.getNavigationArray(item.libraryId, item.seriesId, item.chapterId, item.seriesFormat), {queryParams: params});
  }


  handleReadingListActionCallback(result: ActionResult<ReadingList>) {
    switch (result.effect) {
      case 'update':
        this.readingList.set({...result.entity});
        break;
      case 'remove':
        this.router.navigateByUrl('/lists');
        break;
      case 'reload':
        this.router.navigateByUrl(`/list/${this.readingListId()}`);
        break;
      case 'none':
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
    if (!readingList) return;
    this.actionService.editReadingList(readingList, (readingList: ReadingList) => {
      // Reload information around list
      this.readingListService.getReadingList(this.readingListId()).subscribe(rl => {
        this.readingList.set(rl!);
      });
    });
  }

  orderUpdated(event: IndexUpdateEvent) {
    if (!this.readingList()) return;
    this.readingListService.updatePosition(this.readingList().id, event.item.id, event.fromPosition, event.toPosition).subscribe(() => {
      this.getListItems();
    });
  }

  removeItem(removeEvent: ItemRemoveEvent) {
    if (!this.readingList()) return;
    this.readingListService.deleteItem(this.readingList().id, removeEvent.item.id).subscribe(() => {
      const currentItems = this.items();
      const updated = [...currentItems];

      updated.splice(removeEvent.position, 1);

      this.items.set(updated);
      this.toastr.success(translate('toasts.item-removed'));
    });
  }

  async removeRead() {
    if (!this.readingList()) return;

    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-read-from-readinglist'))) return;

    this.isLoading.set(true);
    this.readingListService.removeRead(this.readingList().id).subscribe((resp) => {
      if (resp === 'Nothing to remove') {
        this.toastr.info(translate('toasts.nothing-to-remove'));
        return;
      }

      this.getListItems();
    });
  }

  read(incognitoMode: boolean = false) {
    if (!this.readingList()) return;
    const firstItem = this.items()[0];
    this.router.navigate(
      this.readerService.getNavigationArray(firstItem.libraryId, firstItem.seriesId, firstItem.chapterId, firstItem.seriesFormat),
      {queryParams: {readingListId: this.readingList().id, incognitoMode: incognitoMode}});
  }

  continue(incognitoMode: boolean = false) {
    if (!this.readingList()) return;
    const currentItems = this.items();
    let currentlyReadingChapter = currentItems[0];
    for (let i = 0; i < currentItems.length; i++) {
      if (currentItems[i].pagesRead >= currentItems[i].pagesTotal) {
        continue;
      }
      currentlyReadingChapter = currentItems[i];
      break;
    }

    this.router.navigate(
      this.readerService.getNavigationArray(currentlyReadingChapter.libraryId, currentlyReadingChapter.seriesId, currentlyReadingChapter.chapterId, currentlyReadingChapter.seriesFormat),
      {queryParams: {readingListId: this.readingList().id, incognitoMode: incognitoMode}});
  }

  toggleReorder() {
    this.formGroup.get('edit')?.setValue(!this.formGroup.get('edit')!.value);
  }


  onNavChange(event: NgbNavChangeEvent) {
    this.updateUrl(event.nextId);
  }

  private updateUrl(activeTab: Tabs) {
    const tokens = this.location.path().split('#');
    const newUrl = `${tokens[0]}#${activeTab}`;
    this.location.replaceState(newUrl)
  }

  switchTabsToDetail() {
    this.activeTabId = Tabs.Details;
    setTimeout(() => {
      const tabElem = this.document.querySelector('#details-tab');
      if (tabElem) {
        (tabElem as HTMLLIElement).scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'center' });
      }
    }, 10);
  }

  openFilter(field: FilterField, value: string | number) {
    this.filterUtilityService.applyFilter(['lists', this.readingList().id], field, FilterComparison.Equal, `${value}`).subscribe();
  }

  protected readonly Breakpoint = Breakpoint;
  protected readonly FilterField = FilterField;
}
