import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { Series } from 'src/app/_models/series';
import { ImageService } from 'src/app/_services/image.service';
import { ReaderService } from 'src/app/_services/reader.service';
import { SeriesService } from 'src/app/_services/series.service';
import { PageBookmark } from '../../_models/page-bookmark';

@Component({
  selector: 'app-bookmark',
  templateUrl: './bookmark.component.html',
  styleUrls: ['./bookmark.component.scss']
})
export class BookmarkComponent implements OnInit {

  @Input() bookmark: PageBookmark | undefined;
  @Output() bookmarkRemoved: EventEmitter<PageBookmark> = new EventEmitter<PageBookmark>();
  series: Series | undefined;

  isClearing: boolean = false;
  isDownloading: boolean = false;

  constructor(public imageService: ImageService, private seriesService: SeriesService, private readerService: ReaderService) { }

  ngOnInit(): void {
    if (this.bookmark) {
      this.seriesService.getSeries(this.bookmark.seriesId).subscribe(series => {
        this.series = series;
      });
    }
  }

  handleClick(event: any) {

  }

  removeBookmark() {
    if (this.bookmark === undefined) return;
    this.readerService.unbookmark(this.bookmark.seriesId, this.bookmark.volumeId, this.bookmark.chapterId, this.bookmark.page).subscribe(res => {
      this.bookmarkRemoved.emit(this.bookmark);
      this.bookmark = undefined;
    });
  }
}
