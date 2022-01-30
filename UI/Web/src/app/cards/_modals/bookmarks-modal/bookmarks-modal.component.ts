import { Component, Input, OnInit } from '@angular/core';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { finalize, take, takeWhile } from 'rxjs/operators';
import { DownloadService } from 'src/app/shared/_services/download.service';
import { PageBookmark } from 'src/app/_models/page-bookmark';
import { Series } from 'src/app/_models/series';
import { ImageService } from 'src/app/_services/image.service';
import { ReaderService } from 'src/app/_services/reader.service';
import { SeriesService } from 'src/app/_services/series.service';

@Component({
  selector: 'app-bookmarks-modal',
  templateUrl: './bookmarks-modal.component.html',
  styleUrls: ['./bookmarks-modal.component.scss']
})
export class BookmarksModalComponent implements OnInit {

  @Input() series!: Series;

  bookmarks: Array<PageBookmark> = [];
  title: string = '';
  subtitle: string = '';
  isDownloading: boolean = false;
  isClearing: boolean = false;

  uniqueChapters: number = 0;

  constructor(public imageService: ImageService, private readerService: ReaderService, 
    public modal: NgbActiveModal, private downloadService: DownloadService, 
    private toastr: ToastrService, private seriesService: SeriesService) { }

  ngOnInit(): void {
    this.init();
  }

  init() {
    this.readerService.getBookmarksForSeries(this.series.id).pipe(take(1)).subscribe(bookmarks => {
      this.bookmarks = bookmarks;
      const chapters: {[id: number]: string} = {};
      this.bookmarks.forEach(bmk => {
        if (!chapters.hasOwnProperty(bmk.chapterId)) {
          chapters[bmk.chapterId] = '';
        }
      });
      this.uniqueChapters = Object.keys(chapters).length;
    });
  }

  close() {
    this.modal.close();
  }

  removeBookmark(bookmark: PageBookmark, index: number) {
    this.bookmarks.splice(index, 1);
  }

  downloadBookmarks() {
    this.isDownloading = true;
    this.downloadService.downloadBookmarks(this.bookmarks).pipe(
      takeWhile(val => {
        return val.state != 'DONE';
      }),
      finalize(() => {
        this.isDownloading = false;
      })).subscribe(() => {/* No Operation */});
  }

  clearBookmarks() {
    this.isClearing = true;
    this.readerService.clearBookmarks(this.series.id).subscribe(() => {
      this.isClearing = false;
      this.init();
      this.toastr.success(this.series.name + '\'s bookmarks have been removed');
    });
  }

}
