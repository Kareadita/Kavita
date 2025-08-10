import {ChangeDetectionStrategy, Component, effect, EventEmitter, inject, model} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {NgbActiveOffcanvas} from "@ng-bootstrap/ng-bootstrap";
import {ReaderService} from "../../../../_services/reader.service";
import {PageBookmark} from "../../../../_models/readers/page-bookmark";
import {ImageService} from "../../../../_services/image.service";
import {VirtualScrollerModule} from "@iharbeck/ngx-virtual-scroller";
import {ImageComponent} from "../../../../shared/image/image.component";

@Component({
  selector: 'app-view-bookmarks-drawer',
  imports: [
    TranslocoDirective,
    VirtualScrollerModule,
    ImageComponent
  ],
  templateUrl: './view-bookmark-drawer.component.html',
  styleUrl: './view-bookmark-drawer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ViewBookmarkDrawerComponent {
  private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
  private readonly readerService = inject(ReaderService);
  protected readonly imageService = inject(ImageService);

  chapterId = model<number>();
  bookmarks = model<PageBookmark[]>();
  loadPage: EventEmitter<PageBookmark | null> = new EventEmitter<PageBookmark | null>();
  /**
   * Emitted when a bookmark is removed
   */
  removeBookmark: EventEmitter<PageBookmark> = new EventEmitter<PageBookmark>();



  constructor() {
    effect(() => {
      const id = this.chapterId();
      if (!id) {
        console.error('You must pass chapterId');
        return;
      }

      this.readerService.getBookmarks(id).subscribe(bookmarks => {
        this.bookmarks.set(bookmarks.sort((a, b) => a.page - b.page));
      });
    });
  }

  goToBookmark(bookmark: PageBookmark) {
    const bookmarkCopy = {...bookmark};
    bookmarkCopy.xPath = this.readerService.scopeBookReaderXpath(bookmarkCopy.xPath ?? '');

    this.loadPage.emit(bookmarkCopy);
  }

  deleteBookmark(bookmark: PageBookmark) {
    this.readerService.unbookmark(bookmark.seriesId, bookmark.volumeId, bookmark.chapterId, bookmark.page, bookmark.imageOffset).subscribe(_ => {
      const bmarks = this.bookmarks() ?? [];
      this.bookmarks.set(bmarks.filter(b => b.id !== bookmark.id));
      // Inform UI to inject/refresh image bookmark icons
      this.removeBookmark.emit(bookmark);
    });
  }


  close() {
    this.activeOffcanvas.close();
  }
}
