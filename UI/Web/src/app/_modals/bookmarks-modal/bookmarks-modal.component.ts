import { Component, Input, OnInit } from '@angular/core';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { take } from 'rxjs/operators';
import { ChapterInfo } from 'src/app/manga-reader/_models/chapter-info';
import { DownloadService } from 'src/app/shared/_services/download.service';
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

  @Input() type!: 'series' | 'volume' | 'chapter';
  @Input() entity!: Series | Volume | Chapter;

  bookmarks: Array<PageBookmark> = [];
  title: string = '';
  subtitle: string = '';
  isDownloading: boolean = false;
  isClearing: boolean = false;

  constructor(public imageService: ImageService, private readerService: ReaderService, public modal: NgbActiveModal, private downloadService: DownloadService) { }

  ngOnInit(): void {
    let chapterId = 0;
    if (this.type === 'volume' && this.entity !== undefined) {
      const vol = <Volume>this.entity;
      if (vol.chapters) {
        chapterId = vol.chapters[0].id;
      }
    } else if (this.type == 'chapter') {
      chapterId = this.entity.id;
    } else {
      const series = (this.entity as Series);
      if (series.volumes.length > 0 && series.volumes[0].chapters) {
        chapterId = series.volumes[0].chapters[0].id;
      }
    }

    this.readerService.getChapterInfo(chapterId).pipe(take(1)).subscribe(chapterInfo => {
      this.updateTitle(chapterInfo);
    });
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

  downloadBookmarks() {
    this.isDownloading = true;
    //this.downloadService.downloadBookmarks(this.bookmarks.map(bmk => bmk.id)).pipe(take(1)).subscribe(() => {});

  }

  clearBookmarks() {
    this.isClearing = true;
  }

}
