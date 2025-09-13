import {Pipe, PipeTransform} from '@angular/core';
import {Annotation} from "../book-reader/_models/annotations/annotation";
import {translate} from "@jsverse/transloco";

/**
 * Responsible to create a --Page X, Chapter Y
 */
@Pipe({
  name: 'pageChapterLabel'
})
export class PageChapterLabelPipe implements PipeTransform {

  transform(annotation: Annotation): string {
    const pageNumber = annotation.pageNumber;
    const chapterTitle = annotation.chapterTitle ?? '';

    if (chapterTitle === '') return translate('page-chapter-label-pipe.page-only', {pageNumber});
    return translate('page-chapter-label-pipe.full', {pageNumber, chapterTitle});
  }

}
