import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { BookChapterItem } from '../../_models/book-chapter-item';
import { NgIf, NgFor } from '@angular/common';
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
    selector: 'app-table-of-contents',
    templateUrl: './table-of-contents.component.html',
    styleUrls: ['./table-of-contents.component.scss'],
    changeDetection: ChangeDetectionStrategy.Default,
    standalone: true,
  imports: [NgIf, NgFor, TranslocoDirective]
})
export class TableOfContentsComponent  {

  @Input({required: true}) chapterId!: number;
  @Input({required: true}) pageNum!: number;
  @Input({required: true}) currentPageAnchor!: string;
  @Input() chapters:Array<BookChapterItem> = [];

  @Output() loadChapter: EventEmitter<{pageNum: number, part: string}> = new EventEmitter();

  constructor() {}

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
