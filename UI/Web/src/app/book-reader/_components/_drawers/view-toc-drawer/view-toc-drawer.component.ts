import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  effect,
  EventEmitter,
  inject,
  input,
  model,
  signal
} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {NgbActiveOffcanvas} from "@ng-bootstrap/ng-bootstrap";
import {TableOfContentsComponent} from "../../table-of-contents/table-of-contents.component";
import {BookChapterItem} from "../../../_models/book-chapter-item";
import {BookService} from "../../../_services/book.service";
import {LoadPageEvent} from "../view-bookmarks-drawer/view-bookmark-drawer.component";


@Component({
  selector: 'app-view-toc-drawer',
  imports: [
    TranslocoDirective,
    TableOfContentsComponent
  ],
  templateUrl: './view-toc-drawer.component.html',
  styleUrl: './view-toc-drawer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ViewTocDrawerComponent {
  private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly bookService = inject(BookService);

  chapterId = input.required<number>();
  /**
   * Current Page
   */
  pageNum = model.required<number>();

  /**
   * The actual pages from the epub, used for showing on table of contents. This must be here as we need access to it for scroll anchors
   */
  chapters = signal<Array<BookChapterItem>>([]);
  loading = signal(true);


  loadPage: EventEmitter<LoadPageEvent | null> = new EventEmitter<LoadPageEvent | null>();

  constructor() {

    effect(() => {
      const id = this.chapterId();
      if (!id) {
        console.error('You must pass chapterId');
        return;
      }

      this.bookService.getBookChapters(id).subscribe(bookChapters => {
        this.loading.set(false);
        this.chapters.set(bookChapters);
        this.cdRef.markForCheck();
      });
    });
  }

  loadChapterPage(event: {pageNum: number, part: string}) {
    const part = event.part.length === 0 ? '' : `id("${event.part}")`;
    const evt = {pageNumber: event.pageNum, part: part} as LoadPageEvent;
    this.loadPage.emit(evt);
  }


  close() {
    this.activeOffcanvas.close();
  }
}
