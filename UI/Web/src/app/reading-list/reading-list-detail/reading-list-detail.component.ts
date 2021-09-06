import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
import { UtilityService } from 'src/app/shared/_services/utility.service';
import { Chapter } from 'src/app/_models/chapter';
import { MangaFormat } from 'src/app/_models/manga-format';
import { ReadingList, ReadingListItem } from 'src/app/_models/reading-list';
import { AccountService } from 'src/app/_services/account.service';
import { Action, ActionFactoryService, ActionItem } from 'src/app/_services/action-factory.service';
import { ActionService } from 'src/app/_services/action.service';
import { ImageService } from 'src/app/_services/image.service';
import { ReadingListService } from 'src/app/_services/reading-list.service';
import { IndexUpdateEvent } from '../dragable-ordered-list/dragable-ordered-list.component';

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

  get MangaFormat(): typeof MangaFormat {
    return MangaFormat;
  }

  constructor(private route: ActivatedRoute, private router: Router, private readingListService: ReadingListService,
    private actionService: ActionService, private actionFactoryService: ActionFactoryService, public utilityService: UtilityService,
    public imageService: ImageService, private accountService: AccountService, private toastr: ToastrService) {}

  ngOnInit(): void {
    const listId = this.route.snapshot.paramMap.get('id');

    if (listId === null) {
      this.router.navigateByUrl('/libraries');
      return;
    }

    this.listId = parseInt(listId, 10);

    this.readingListService.getReadingList(this.listId).subscribe(readingList => {
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
          
          this.actions = this.actionFactoryService.getReadingListActions(this.handleReadingListActionCallback.bind(this)).filter(actions => {
            if (actions.action != Action.Edit) return true;
            else if (this.readingList?.promoted && this.isAdmin) return true;
            return false;
            //return actions.action != Action.Edit || (actions.action === Action.Edit && this.readingList.promoted && this.isAdmin);
          });
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
    // TODO: Try to move performAction into the actionables component. (have default handler in the component, allow for overridding to pass additional context)
    if (typeof action.callback === 'function') {
      action.callback(action.action, this.readingList);
    }
  }

  handleReadingListActionCallback(action: Action, readingList: ReadingList) {
    switch(action) {
      case Action.Delete:
        this.readingListService.delete(readingList.id).subscribe(() => {
          this.toastr.success('Reading list deleted');
          this.router.navigateByUrl('library#lists');
        });
    }
  }

  formatTitle(item: ReadingListItem) {
    if (item.chapterNumber === '0') {
      return 'Volume ' + item.volumeNumber;
    }

    if (item.seriesFormat === MangaFormat.EPUB) {
      return 'Volume ' + this.utilityService.cleanSpecialTitle(item.chapterNumber);
    }

    return 'Chapter ' + item.chapterNumber;
  }

  orderUpdated(event: IndexUpdateEvent) {
    this.readingListService.updatePosition(this.readingList.id, event.item.id, event.fromPosition, event.toPosition).subscribe(() => { /* No Operation */ });
  }

  removeItem(item: ReadingListItem, position: number) {
    this.readingListService.deleteItem(this.readingList.id, item.id).subscribe(() => {
      this.items.splice(position, 1);
      this.toastr.success('Item removed');
    });
  }

  removeRead() {
    this.isLoading = true;
    this.readingListService.removeRead(this.readingList.id).subscribe(() => {
      this.getListItems();
    });
  }
}
