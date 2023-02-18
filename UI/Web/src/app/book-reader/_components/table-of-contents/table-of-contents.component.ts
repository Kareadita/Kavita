import { ChangeDetectionStrategy, Component, EventEmitter, Input, OnDestroy, Output } from '@angular/core';
import { Subject } from 'rxjs';
import { BookChapterItem } from '../../_models/book-chapter-item';

@Component({
  selector: 'app-table-of-contents',
  templateUrl: './table-of-contents.component.html',
  styleUrls: ['./table-of-contents.component.scss'],
  changeDetection: ChangeDetectionStrategy.Default
})
export class TableOfContentsComponent implements OnDestroy {

  @Input() chapterId!: number;
  @Input() pageNum!: number;
  @Input() currentPageAnchor!: string;
  @Input() chapters:Array<BookChapterItem> = [];

  @Output() loadChapter: EventEmitter<{pageNum: number, part: string}> = new EventEmitter();

  private onDestroy: Subject<void> = new Subject();

  pageAnchors: {[n: string]: number } = {};

  constructor() {}

  ngOnDestroy(): void {
      this.onDestroy.next();
      this.onDestroy.complete();
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
}
