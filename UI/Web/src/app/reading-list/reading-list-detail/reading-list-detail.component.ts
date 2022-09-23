import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { shareReplay, take } from 'rxjs/operators';
import { ConfirmService } from 'src/app/shared/confirm.service';
import { UtilityService } from 'src/app/shared/_services/utility.service';
import { LibraryType } from 'src/app/_models/library';
import { MangaFormat } from 'src/app/_models/manga-format';
import { ReadingList, ReadingListItem } from 'src/app/_models/reading-list';
import { AccountService } from 'src/app/_services/account.service';
import { Action, ActionFactoryService, ActionItem } from 'src/app/_services/action-factory.service';
import { ActionService } from 'src/app/_services/action.service';
import { ImageService } from 'src/app/_services/image.service';
import { ReadingListService } from 'src/app/_services/reading-list.service';
import { IndexUpdateEvent, ItemRemoveEvent } from '../draggable-ordered-list/draggable-ordered-list.component';
import { LibraryService } from '../../_services/library.service';
import { forkJoin } from 'rxjs';
import { ReaderService } from 'src/app/_services/reader.service';

@Component({
  selector: 'app-reading-list-detail',
  templateUrl: './reading-list-detail.component.html',
  styleUrls: ['./reading-list-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReadingListDetailComponent implements OnInit {
  items: Array<ReadingListItem> = [];
  listId!: number;
  readingList: ReadingList | undefined;
  actions: Array<ActionItem<any>> = [];
  isAdmin: boolean = false;
  isLoading: boolean = false;
  accessibilityMode: boolean = false;

  // Downloading
  hasDownloadingRole: boolean = false;
  downloadInProgress: boolean = false;

  readingListSummary: string = '';

  libraryTypes: {[key: number]: LibraryType} = {};

  readingListImage: string = '';

  get MangaFormat(): typeof MangaFormat {
    return MangaFormat;
  }

  constructor(private route: ActivatedRoute, private router: Router, private readingListService: ReadingListService,
    private actionService: ActionService, private actionFactoryService: ActionFactoryService, public utilityService: UtilityService,
    public imageService: ImageService, private accountService: AccountService, private toastr: ToastrService, 
    private confirmService: ConfirmService, private libraryService: LibraryService, private readerService: ReaderService,
    private readonly cdRef: ChangeDetectorRef) {}

  ngOnInit(): void {
    const listId = this.route.snapshot.paramMap.get('id');

    if (listId === null) {
      this.router.navigateByUrl('/libraries');
      return;
    }
    this.listId = parseInt(listId, 10);

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
        this.toastr.error('This list doesn\'t exist.');
        this.router.navigateByUrl('library');
        return;
      }
      this.readingList = readingList;
      this.readingListSummary = (this.readingList.summary === null ? '' : this.readingList.summary).replace(/\n/g, '<br>');

      this.cdRef.markForCheck();

      this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
        if (user) {
          this.isAdmin = this.accountService.hasAdminRole(user);
          this.hasDownloadingRole = this.accountService.hasDownloadRole(user);
          
          this.actions = this.actionFactoryService.getReadingListActions(this.handleReadingListActionCallback.bind(this)).filter(action => this.readingListService.actionListFilter(action, readingList, this.isAdmin));
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
      this.items = items;
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
    let reader = 'manga';
    if (!this.readingList) return;
    if (item.seriesFormat === MangaFormat.EPUB) {
      reader = 'book;'
    }
    const params = this.readerService.getQueryParamsObject(false, true, this.readingList.id);
    this.router.navigate(this.readerService.getNavigationArray(item.libraryId, item.seriesId, item.chapterId, item.seriesFormat), {queryParams: params});
  }

  handleReadingListActionCallback(action: ActionItem<ReadingList>, readingList: ReadingList) {
    switch(action.action) {
      case Action.Delete:
        this.deleteList(readingList);
        break;
      case Action.Edit:
        this.actionService.editReadingList(readingList, (readingList: ReadingList) => {
          // Reload information around list
          this.readingList = readingList;
          this.readingListSummary = (this.readingList.summary === null ? '' : this.readingList.summary).replace(/\n/g, '<br>');
          this.cdRef.markForCheck();
        });
        break;
    }
  }

  async deleteList(readingList: ReadingList) {
    if (!await this.confirmService.confirm('Are you sure you want to delete the reading list? This cannot be undone.')) return;

    this.readingListService.delete(readingList.id).subscribe(() => {
      this.toastr.success('Reading list deleted');
      this.router.navigateByUrl('library#lists');
    });
  }

  orderUpdated(event: IndexUpdateEvent) {
    if (!this.readingList) return;
    this.readingListService.updatePosition(this.readingList.id, event.item.id, event.fromPosition, event.toPosition).subscribe(() => { /* No Operation */ });
  }

  itemRemoved(event: ItemRemoveEvent) {
    if (!this.readingList) return;
    this.readingListService.deleteItem(this.readingList.id, event.item.id).subscribe(() => {
      this.items.splice(event.position, 1);
      this.cdRef.markForCheck();
      this.toastr.success('Item removed');
    });
  }

  removeRead() {
    if (!this.readingList) return;
    this.isLoading = true;
    this.cdRef.markForCheck();
    this.readingListService.removeRead(this.readingList.id).subscribe((resp) => {
      if (resp === 'Nothing to remove') {
        this.toastr.info(resp);
        return;
      }
      this.getListItems();
    });
  }

  read() {
    if (!this.readingList) return;
    const firstItem = this.items[0];
    this.router.navigate(this.readerService.getNavigationArray(firstItem.libraryId, firstItem.seriesId, firstItem.chapterId, firstItem.seriesFormat), {queryParams: {readingListId: this.readingList.id}});
  }

  continue() {
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

    this.router.navigate(this.readerService.getNavigationArray(currentlyReadingChapter.libraryId, currentlyReadingChapter.seriesId, currentlyReadingChapter.chapterId, currentlyReadingChapter.seriesFormat), {queryParams: {readingListId: this.readingList.id}});
  }
}
