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
import {
  NgbActiveOffcanvas,
  NgbNav,
  NgbNavContent,
  NgbNavItem,
  NgbNavLink,
  NgbNavOutlet
} from "@ng-bootstrap/ng-bootstrap";
import {
  PersonalTableOfContentsComponent,
  PersonalToCEvent
} from "../../personal-table-of-contents/personal-table-of-contents.component";
import {TableOfContentsComponent} from "../../table-of-contents/table-of-contents.component";
import {BookChapterItem} from "../../../_models/book-chapter-item";
import {BookService} from "../../../_services/book.service";


enum TabID {
  TableOfContents = 1,
  PersonalTableOfContents = 2
}


export interface LoadPageEvent {
  pageNumber: number;
  part: string;
}


@Component({
  selector: 'app-view-toc-drawer',
  imports: [
    TranslocoDirective,
    PersonalTableOfContentsComponent,
    NgbNav,
    NgbNavContent,
    NgbNavLink,
    TableOfContentsComponent,
    NgbNavOutlet,
    NgbNavItem
  ],
  templateUrl: './view-toc-drawer.component.html',
  styleUrl: './view-toc-drawer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ViewTocDrawerComponent {
  private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly bookService = inject(BookService);

  chapterId = model<number>();
  /**
   * Current Page
   */
  pageNum = model.required<number>();

  /**
   * Sub Nav tab id
   */
  tocId: TabID = TabID.TableOfContents;
  /**
   * The actual pages from the epub, used for showing on table of contents. This must be here as we need access to it for scroll anchors
   */
  chapters = model<Array<BookChapterItem>>([]);

  /**
   * A anchors that map to the page number. When you click on one of these, we will load a given page up for the user.
   */
  pageAnchors: {[n: string]: number } = {};
  currentPageAnchor: string = '';

  protected readonly TabID = TabID;

  /**
   * Used to refresh the Personal PoC
   */
  refreshPToC: EventEmitter<void> = new EventEmitter<void>();

  loadPage: EventEmitter<LoadPageEvent | null> = new EventEmitter<LoadPageEvent | null>();

  constructor() {

    effect(() => {
      const id = this.chapterId();
      if (!id) {
        console.error('You must pass chapterId');
        return;
      }

      this.bookService.getBookChapters(id).subscribe(bookChapters => {
        this.chapters.set(bookChapters);
        this.cdRef.markForCheck();
      });
    });
  }

  /**
   * From personal table of contents/bookmark
   * @param event
   */
  loadChapterPart(event: PersonalToCEvent) {
    const evt = {pageNumber: event.pageNum, part:event.scrollPart} as LoadPageEvent;
    this.loadPage.emit(evt);
  }

  loadChapterPage(event: {pageNum: number, part: string}) {
    const evt = {pageNumber: event.pageNum, part: `id("${event.part}")`} as LoadPageEvent;
    this.loadPage.emit(evt);
  }


  close() {
    this.activeOffcanvas.close();
  }
}
