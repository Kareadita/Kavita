import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  EventEmitter,
  inject,
  Input,
  OnChanges,
  Output,
  SimpleChanges
} from '@angular/core';
import {BookChapterItem} from '../../_models/book-chapter-item';
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
  selector: 'app-table-of-contents',
  templateUrl: './table-of-contents.component.html',
  styleUrls: ['./table-of-contents.component.scss'],
  imports: [TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.Default,
})
export class TableOfContentsComponent implements OnChanges {

  private readonly cdRef = inject(ChangeDetectorRef);

  @Input({required: true}) chapterId!: number;
  @Input({required: true}) pageNum!: number;
  @Input({required: true}) currentPageAnchor!: string;
  @Input() chapters:Array<BookChapterItem> = [];

  @Output() loadChapter: EventEmitter<{pageNum: number, part: string}> = new EventEmitter();

  ngOnChanges(changes: SimpleChanges) {
    //console.log('Current Page: ', this.pageNum, this.currentPageAnchor);
    this.cdRef.markForCheck();
  }

  cleanIdSelector(id: string) {
    const tokens = id.split('/');
    if (tokens.length > 0) {
      return tokens[0];
    }
    return id;
  }

  loadChapterPage(pageNum: number, part: string) {
    this.loadChapter.emit({pageNum, part});
  }

  isChapterSelected(chapterGroup: BookChapterItem) {
    if (chapterGroup.page === this.pageNum) {
      return true;
    }

    const idx = this.chapters.indexOf(chapterGroup);
    if (idx < 0) {
      return false; // should never happen
    }

    const nextIdx = idx + 1;
    // Last chapter
    if (nextIdx >= this.chapters.length) {
      return chapterGroup.page < this.pageNum;
    }

    // Passed chapter, and next chapter has not been reached
    const next = this.chapters[nextIdx];
    return chapterGroup.page < this.pageNum && next.page > this.pageNum;
  }

  isAnchorSelected(chapter: BookChapterItem) {
    return this.cleanIdSelector(chapter.part) === this.currentPageAnchor
  }

}
