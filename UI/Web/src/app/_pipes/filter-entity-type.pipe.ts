import {inject, Pipe, PipeTransform} from '@angular/core';
import {FilterEntityType} from "../_models/metadata/v2/filter-entity-type";
import {TranslocoService} from "@jsverse/transloco";

@Pipe({
  name: 'filterEntityType',
  pure: true,
  standalone: true
})
export class FilterEntityTypePipe implements PipeTransform {
  private readonly translocoService = inject(TranslocoService);

  transform(value: FilterEntityType): string {
    switch (value) {
      case FilterEntityType.Series:
        return this.translocoService.translate('filter-entity-type-pipe.series');
      case FilterEntityType.ReadingList:
        return this.translocoService.translate('filter-entity-type-pipe.reading-list');
      case FilterEntityType.Person:
        return this.translocoService.translate('filter-entity-type-pipe.person');
      case FilterEntityType.Annotation:
        return this.translocoService.translate('filter-entity-type-pipe.annotation');
    }
  }

}
