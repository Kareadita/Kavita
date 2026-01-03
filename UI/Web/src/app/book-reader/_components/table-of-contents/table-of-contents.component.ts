import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  EventEmitter,
  inject,
  model,
  Output,
  signal
} from '@angular/core';
import {BookChapterItem} from '../../_models/book-chapter-item';
import {TranslocoDirective} from "@jsverse/transloco";
import {LoadingComponent} from "../../../shared/loading/loading.component";
import {DOCUMENT} from "@angular/common";

@Component({
  selector: 'app-table-of-contents',
  templateUrl: './table-of-contents.component.html',
  styleUrls: ['./table-of-contents.component.scss'],
  imports: [TranslocoDirective, LoadingComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TableOfContentsComponent {

  private readonly document = inject(DOCUMENT)

  chapterId = model.required<number>();
  pageNum = model.required<number>();
  currentPageAnchor = signal<string>('');
  chapters = model.required<Array<BookChapterItem>>();
  loading = model.required<boolean>();

  displayedChapters = computed(() => {
    const chapters = this.chapters();
    if (chapters.length === 1) {
      return chapters[0].children;
    }

    return chapters;
  });
  isDisplayingChildrenOnly = computed(() => this.chapters().length === 1);


  @Output() loadChapter: EventEmitter<{pageNum: number, part: string}> = new EventEmitter();

  constructor() {
    effect(() => {
      const selectedChapterIdx = this.displayedChapters()
        .findIndex(chapter => this.isChapterSelected(chapter));

      if (selectedChapterIdx < 0) return;

      setTimeout(() => {
        const chapterElement = this.document.getElementById(`${selectedChapterIdx}`);
        if (chapterElement) {
          chapterElement.scrollIntoView({behavior: 'smooth'});
        }
      }, 10); // Some delay to allow the items to be rendered into the DOM
    });
  }

  cleanIdSelector(id: string) {
    const tokens = id.split('/');
    if (tokens.length > 0) {
      return tokens[0];
    }
    return id;
  }

  loadChapterPage(pageNum: number, part: string) {
    this.pageNum.set(pageNum);
    this.currentPageAnchor.set(part);

    this.loadChapter.emit({pageNum, part});
  }

  isChapterSelected(chapterGroup: BookChapterItem) {
    const currentPageNum = this.pageNum();
    const chapters = this.displayedChapters();

    if (chapterGroup.page === currentPageNum) {
      return true;
    }

    const idx = chapters.indexOf(chapterGroup);
    if (idx < 0) {
      return false; // should never happen
    }

    const nextIdx = idx + 1;
    // Last chapter
    if (nextIdx >= chapters.length) {
      return chapterGroup.page < currentPageNum;
    }

    // Passed chapter, and next chapter has not been reached
    const next = chapters[nextIdx];
    return chapterGroup.page < currentPageNum && next.page > currentPageNum;
  }

  isAnchorSelected(chapter: BookChapterItem) {
    return this.cleanIdSelector(chapter.part) === this.currentPageAnchor();
  }

}
