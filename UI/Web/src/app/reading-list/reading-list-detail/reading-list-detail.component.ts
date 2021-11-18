import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
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
import { IndexUpdateEvent, ItemRemoveEvent } from '../dragable-ordered-list/dragable-ordered-list.component';
import { LibraryService } from '../../_services/library.service';
import { forkJoin } from 'rxjs';

@Component({
  selector: 'app-reading-list-detail',
  templateUrl: './reading-list-detail.component.html',
  styleUrls: ['./reading-list-detail.component.scss']
})
export class ReadingListDetailComponent implements OnInit {
  items: Array<ReadingListItem> = [];
  listId!: number;
  readingList!: ReadingList;
  actions: Array<ActionItem<any>> = [];
  isAdmin: boolean = false;
  isLoading: boolean = false;
  accessibilityMode: boolean = false;

  // Downloading
  hasDownloadingRole: boolean = false;
  downloadInProgress: boolean = false;

  libraryTypes: {[key: number]: LibraryType} = {};

  get MangaFormat(): typeof MangaFormat {
    return MangaFormat;
  }

  constructor(private route: ActivatedRoute, private router: Router, private readingListService: ReadingListService,
    private actionService: ActionService, private actionFactoryService: ActionFactoryService, public utilityService: UtilityService,
    public imageService: ImageService, private accountService: AccountService, private toastr: ToastrService, 
    private confirmService: ConfirmService, private libraryService: LibraryService) {}

  ngOnInit(): void {
    const listId = this.route.snapshot.paramMap.get('id');

    if (listId === null) {
      this.router.navigateByUrl('/libraries');
      return;
    }

    this.listId = parseInt(listId, 10);

    this.libraryService.getLibraries().subscribe(libs => {
      
    });

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

      this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
        if (user) {
          this.isAdmin = this.accountService.hasAdminRole(user);
          this.hasDownloadingRole = this.accountService.hasDownloadRole(user);
          
          this.actions = this.actionFactoryService.getReadingListActions(this.handleReadingListActionCallback.bind(this)).filter(action => this.readingListService.actionListFilter(action, readingList, this.isAdmin));
        }
      });
    });
    this.getListItems();
  }

  getListItems() {
    this.isLoading = true;
    this.readingListService.getListItems(this.listId).subscribe(items => {
      this.items = items;
      this.isLoading = false;
    });
  }

  performAction(action: ActionItem<any>) {
    if (typeof action.callback === 'function') {
      action.callback(action.action, this.readingList);
    }
  }

  handleReadingListActionCallback(action: Action, readingList: ReadingList) {
    switch(action) {
      case Action.Delete:
        this.deleteList(readingList);
        break;
      case Action.Edit:
        this.actionService.editReadingList(readingList, (readingList: ReadingList) => {
          // Reload information around list
          this.readingList = readingList;
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

  formatTitle(item: ReadingListItem) {
    if (item.chapterNumber === '0') {
      return 'Volume ' + item.volumeNumber;
    }

    if (item.seriesFormat === MangaFormat.EPUB) {
      return 'Volume ' + this.utilityService.cleanSpecialTitle(item.chapterNumber);
    }

    let chapterNum = item.chapterNumber;
    if (!item.chapterNumber.match(/^\d+$/)) {
      chapterNum = this.utilityService.cleanSpecialTitle(item.chapterNumber);
    }

    return this.utilityService.formatChapterName(this.libraryTypes[item.libraryId], true, true) + chapterNum;
  }

  orderUpdated(event: IndexUpdateEvent) {
    this.readingListService.updatePosition(this.readingList.id, event.item.id, event.fromPosition, event.toPosition).subscribe(() => { /* No Operation */ });
  }

  itemRemoved(event: ItemRemoveEvent) {
    this.readingListService.deleteItem(this.readingList.id, event.item.id).subscribe(() => {
      this.items.splice(event.position, 1);
      this.toastr.success('Item removed');
    });
  }

  removeRead() {
    this.isLoading = true;
    this.readingListService.removeRead(this.readingList.id).subscribe(() => {
      this.getListItems();
    });
  }

  read() {
    let currentlyReadingChapter = this.items[0];
    for (let i = 0; i < this.items.length; i++) {
      if (this.items[i].pagesRead >= this.items[i].pagesTotal) {
        continue;
      }
      currentlyReadingChapter = this.items[i];
      break;
    }

    if (currentlyReadingChapter.seriesFormat === MangaFormat.EPUB) {
      this.router.navigate(['library', currentlyReadingChapter.libraryId, 'series', currentlyReadingChapter.seriesId, 'book', currentlyReadingChapter.chapterId], {queryParams: {readingListId: this.readingList.id}});
    } else {
      this.router.navigate(['library', currentlyReadingChapter.libraryId, 'series', currentlyReadingChapter.seriesId, 'manga', currentlyReadingChapter.chapterId], {queryParams: {readingListId: this.readingList.id}});
    }
  }
}
