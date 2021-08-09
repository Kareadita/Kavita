import { Component, Input, OnInit } from '@angular/core';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { take } from 'rxjs/operators';
import { ChapterInfo } from 'src/app/manga-reader/_models/chapter-info';
import { Chapter } from 'src/app/_models/chapter';
import { PageBookmark } from 'src/app/_models/page-bookmark';
import { Series } from 'src/app/_models/series';
import { Volume } from 'src/app/_models/volume';
import { ImageService } from 'src/app/_services/image.service';
import { ReaderService } from 'src/app/_services/reader.service';

@Component({
  selector: 'app-bookmarks-modal',
  templateUrl: './bookmarks-modal.component.html',
  styleUrls: ['./bookmarks-modal.component.scss']
})
export class BookmarksModalComponent implements OnInit {

  @Input() chapterId!: number;
  @Input() type!: 'series' | 'volume' | 'chapter';
  @Input() entity!: Series | Volume | Chapter;

  bookmarks: Array<PageBookmark> = [];
  title: string = '';
  subtitle: string = '';

  constructor(public imageService: ImageService, private readerService: ReaderService, public modal: NgbActiveModal) { }

  ngOnInit(): void {
    // TODO: Ensure book is cached (maybe by loading chapterInfo)
    let chapterId = 0;
    // if (this.type === 'volume') {
    //   chapterId = (this.entity as Volume).chapters
    // };
    // this.readerService.getChapterInfo(this.chapterId).pipe(take(1)).subscribe(chapterInfo => {
    //   this.updateTitle(chapterInfo);

      
    // });

    switch (this.type) {
      case 'chapter':
      {
        this.readerService.getBookmarks(this.entity.id).pipe(take(1)).subscribe(bookmarks => {
          this.bookmarks = bookmarks;
        });
        break;
      }
      case 'volume':
      {
        this.readerService.getBookmarksForVolume(this.entity.id).pipe(take(1)).subscribe(bookmarks => {
          this.bookmarks = bookmarks;
        });
        break;
      }
      case 'series':
      {
        this.readerService.getBookmarksForSeries(this.entity.id).pipe(take(1)).subscribe(bookmarks => {
          this.bookmarks = bookmarks;
        });
        break;
      }
      default:
        break;
    }
  }

  updateTitle(chapterInfo: ChapterInfo) {
    this.title = chapterInfo.seriesName;
    if (chapterInfo.chapterTitle.length > 0) {
      this.title += ' - ' + chapterInfo.chapterTitle;
    }

    this.subtitle = '';
    if (chapterInfo.isSpecial && chapterInfo.volumeNumber === '0') {
      this.subtitle = chapterInfo.fileName;
    } else if (!chapterInfo.isSpecial && chapterInfo.volumeNumber === '0') {
      this.subtitle = 'Chapter ' + chapterInfo.chapterNumber;
    } else {
      this.subtitle = 'Volume ' + chapterInfo.volumeNumber;

      if (chapterInfo.chapterNumber !== '0') {
        this.subtitle += ' Chapter ' + chapterInfo.chapterNumber;
      }
    }
}

  close() {
    this.modal.close({success: false, series: undefined});
  }

}
