import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  effect,
  EventEmitter,
  inject,
  model
} from '@angular/core';
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
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly readerService = inject(ReaderService);
  protected readonly imageService = inject(ImageService);

  chapterId = model<number>();
  bookmarks = model<PageBookmark[]>();
  loadPage: EventEmitter<PageBookmark | null> = new EventEmitter<PageBookmark | null>();



  constructor() {
    effect(() => {
      const id = this.chapterId();
      if (!id) {
        console.error('You must pass chapterId');
        return;
      }

      this.readerService.getBookmarks(id).subscribe(bookmarks => {
        this.bookmarks.set(bookmarks.sort((a, b) => a.page - b.page));
        new Set(this.bookmarks());
        this.cdRef.markForCheck();
      });
    });
  }

  goToBookmark(bookmark: PageBookmark) {
    this.loadPage.emit(bookmark);
  }


  close() {
    this.activeOffcanvas.close();
  }
}
