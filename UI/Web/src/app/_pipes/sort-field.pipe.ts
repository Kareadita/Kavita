import {inject, Pipe, PipeTransform} from '@angular/core';
import {SeriesSortField} from "../_models/metadata/series-filter";
import {TranslocoService} from "@jsverse/transloco";
import {ValidFilterEntity} from "../metadata-filter/filter-settings";
import {PersonSortField} from "../_models/metadata/v2/person-sort-field";
import {AnnotationsSortField} from "../_models/metadata/v2/annotations-filter";
import {ReadingListSortField} from "../_models/metadata/v2/reading-list-sort-field";

@Pipe({
  name: 'sortField',
  standalone: true
})
export class SortFieldPipe implements PipeTransform {
  private translocoService = inject(TranslocoService);


  transform<T extends number>(value: T, entityType: ValidFilterEntity): string {

    switch (entityType) {
      case "annotation":
        return this.getAnnotationSortFields(value as AnnotationsSortField);
      case 'series':
        return this.seriesSortFields(value as SeriesSortField);
      case 'person':
        return this.personSortFields(value as PersonSortField);
      case 'readinglist':
        return this.readingListSortFields(value as ReadingListSortField);

    }
  }

  private getAnnotationSortFields(value: AnnotationsSortField) {
    switch (value) {
      case AnnotationsSortField.Color:
        return this.translocoService.translate('sort-field-pipe.annotation-color');
      case AnnotationsSortField.LastModified:
        return this.translocoService.translate('sort-field-pipe.last-modified');
      case AnnotationsSortField.Owner:
        return this.translocoService.translate('sort-field-pipe.annotation-owner');
      case AnnotationsSortField.Created:
        return this.translocoService.translate('sort-field-pipe.created');
    }
  }

  private personSortFields(value: PersonSortField) {
    switch (value) {
      case PersonSortField.Name:
        return this.translocoService.translate('sort-field-pipe.person-name');
      case PersonSortField.SeriesCount:
        return this.translocoService.translate('sort-field-pipe.person-series-count');
      case PersonSortField.ChapterCount:
        return this.translocoService.translate('sort-field-pipe.person-chapter-count');
    }
  }

  private readingListSortFields(value: ReadingListSortField) {
    switch (value) {
      case ReadingListSortField.ReleaseYearStart:
        return this.translocoService.translate('sort-field-pipe.readinglist-releaseyear-start');
      case ReadingListSortField.ReleaseYearEnd:
        return this.translocoService.translate('sort-field-pipe.readinglist-releaseyear-end');
      case ReadingListSortField.ItemCount:
        return this.translocoService.translate('sort-field-pipe.readinglist-item-count');
      case ReadingListSortField.Title:
        return this.translocoService.translate('sort-field-pipe.readinglist-title');

    }
  }

  private seriesSortFields(value: SeriesSortField) {
    switch (value) {
      case SeriesSortField.SortName:
        return this.translocoService.translate('sort-field-pipe.sort-name');
      case SeriesSortField.Created:
        return this.translocoService.translate('sort-field-pipe.created');
      case SeriesSortField.LastModified:
        return this.translocoService.translate('sort-field-pipe.last-modified');
      case SeriesSortField.LastChapterAdded:
        return this.translocoService.translate('sort-field-pipe.last-chapter-added');
      case SeriesSortField.TimeToRead:
        return this.translocoService.translate('sort-field-pipe.time-to-read');
      case SeriesSortField.ReleaseYear:
        return this.translocoService.translate('sort-field-pipe.release-year');
      case SeriesSortField.ReadProgress:
        return this.translocoService.translate('sort-field-pipe.read-progress');
      case SeriesSortField.AverageRating:
        return this.translocoService.translate('sort-field-pipe.average-rating');
      case SeriesSortField.Random:
        return this.translocoService.translate('sort-field-pipe.random');
      case SeriesSortField.UserRating:
          return this.translocoService.translate('sort-field-pipe.user-rating');
      case SeriesSortField.UnreadChapterCount:
        return this.translocoService.translate('sort-field-pipe.unread-chapter-count');
    }
  }

}
