import { Component, OnInit } from '@angular/core';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { take, takeWhile, finalize } from 'rxjs/operators';
import { BookmarksModalComponent } from 'src/app/cards/_modals/bookmarks-modal/bookmarks-modal.component';
import { DownloadService } from 'src/app/shared/_services/download.service';
import { PageBookmark } from 'src/app/_models/page-bookmark';
import { Series } from 'src/app/_models/series';
import { ReaderService } from 'src/app/_services/reader.service';
import { SeriesService } from 'src/app/_services/series.service';

@Component({
  selector: 'app-series-bookmarks',
  templateUrl: './series-bookmarks.component.html',
  styleUrls: ['./series-bookmarks.component.scss']
})
export class SeriesBookmarksComponent implements OnInit {

  bookmarks: Array<PageBookmark> = [];
  series: Array<Series> = [];
  loadingBookmarks: boolean = false;
  seriesIds: {[id: number]: number} = {};
  isDownloading: boolean = false;
  isClearing: boolean = false;

  constructor(private readerService: ReaderService, private seriesService: SeriesService,
    private modalService: NgbModal, private downloadService: DownloadService, private toastr: ToastrService) { }

  ngOnInit(): void {
    this.loadBookmarks();
  }

  loadBookmarks() {
    this.loadingBookmarks = true;
    this.readerService.getAllBookmarks().pipe(take(1)).subscribe(bookmarks => {
      this.bookmarks = bookmarks;
      this.seriesIds = {};
      this.bookmarks.forEach(bmk => {
        if (!this.seriesIds.hasOwnProperty(bmk.seriesId)) {
          this.seriesIds[bmk.seriesId] = bmk.page;
        } else {
          this.seriesIds[bmk.seriesId] += bmk.page;
        }
      });

      const ids = Object.keys(this.seriesIds).map(k => parseInt(k, 10));
      this.seriesService.getAllSeriesByIds(ids).subscribe(series => {
        this.series = series;
        this.loadingBookmarks = false;
      });
    });
  }

  viewBookmarks(series: Series) {
    const bookmarkModalRef = this.modalService.open(BookmarksModalComponent, { scrollable: true, size: 'lg' });
    bookmarkModalRef.componentInstance.series = series;
    bookmarkModalRef.closed.pipe(take(1)).subscribe(() => {
      this.loadBookmarks();
    });
  }

  clearBookmarks(series: Series) {
    this.isClearing = true;
    this.readerService.clearBookmarks(series.id).subscribe(() => {
      const index = this.series.indexOf(series);
      if (index > -1) {
        this.series.splice(index, 1);
      }
      this.isClearing = false;
      this.toastr.success(series.name + '\'s bookmarks have been removed');
    });
  }

  getBookmarkPages(seriesId: number) {
    return this.seriesIds[seriesId];
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

  

}
