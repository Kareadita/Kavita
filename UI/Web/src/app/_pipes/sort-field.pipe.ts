import {Pipe, PipeTransform} from '@angular/core';
import {SortField} from "../_models/metadata/series-filter";
import {TranslocoService} from "@jsverse/transloco";
import {ValidFilterEntity} from "../metadata-filter/filter-settings";
import {PersonSortField} from "../_models/metadata/v2/person-sort-field";

@Pipe({
  name: 'sortField',
  standalone: true
})
export class SortFieldPipe implements PipeTransform {

  constructor(private translocoService: TranslocoService) {
  }

  transform<T extends number>(value: T, entityType: ValidFilterEntity): string {

    switch (entityType) {
      case 'series':
        return this.seriesSortFields(value as SortField);
      case 'person':
        return this.personSortFields(value as PersonSortField);

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

  private seriesSortFields(value: SortField) {
    switch (value) {
      case SortField.SortName:
        return this.translocoService.translate('sort-field-pipe.sort-name');
      case SortField.Created:
        return this.translocoService.translate('sort-field-pipe.created');
      case SortField.LastModified:
        return this.translocoService.translate('sort-field-pipe.last-modified');
      case SortField.LastChapterAdded:
        return this.translocoService.translate('sort-field-pipe.last-chapter-added');
      case SortField.TimeToRead:
        return this.translocoService.translate('sort-field-pipe.time-to-read');
      case SortField.ReleaseYear:
        return this.translocoService.translate('sort-field-pipe.release-year');
      case SortField.ReadProgress:
        return this.translocoService.translate('sort-field-pipe.read-progress');
      case SortField.AverageRating:
        return this.translocoService.translate('sort-field-pipe.average-rating');
      case SortField.Random:
        return this.translocoService.translate('sort-field-pipe.random');
    }
  }

}
