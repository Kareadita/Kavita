import { Pipe, PipeTransform } from '@angular/core';
import {SortField} from "../_models/metadata/series-filter";
import {TranslocoService} from "@jsverse/transloco";

@Pipe({
  name: 'sortField',
  standalone: true
})
export class SortFieldPipe implements PipeTransform {

  constructor(private translocoService: TranslocoService) {
  }

  transform(value: SortField): string {
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
