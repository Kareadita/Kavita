import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { UtilityService } from 'src/app/shared/_services/utility.service';
import { Chapter } from 'src/app/_models/chapter';
import { MangaFormat } from 'src/app/_models/manga-format';
import { ReadingList, ReadingListItem } from 'src/app/_models/reading-list';
import { Action, ActionFactoryService, ActionItem } from 'src/app/_services/action-factory.service';
import { ActionService } from 'src/app/_services/action.service';
import { ImageService } from 'src/app/_services/image.service';
import { ReadingListService } from 'src/app/_services/reading-list.service';

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

  get MangaFormat(): typeof MangaFormat {
    return MangaFormat;
  }

  constructor(private route: ActivatedRoute, private router: Router, private readingListService: ReadingListService,
    private actionService: ActionService, private actionFactoryService: ActionFactoryService, public utilityService: UtilityService,
    public imageService: ImageService) {}

  ngOnInit(): void {
    const listId = this.route.snapshot.paramMap.get('id');

    if (listId === null) {
      this.router.navigateByUrl('/libraries');
      return;
    }

    this.listId = parseInt(listId, 10);

    this.readingListService.getReadingList(this.listId).subscribe(readingList => {
      this.readingList = readingList;
    });

    this.readingListService.getListItems(this.listId).subscribe(items => {
      this.items = items;
    });

    this.actions = this.actionFactoryService.getReadingListActions(this.handleReadingListActionCallback.bind(this));
  }

  performAction(event: any) {

  }

  handleReadingListActionCallback(action: Action, readingList: ReadingList) {

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
}
