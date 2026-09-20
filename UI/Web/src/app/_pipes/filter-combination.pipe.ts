import {inject, Pipe, PipeTransform} from '@angular/core';
import {FilterCombination} from "../_models/metadata/v2/filter-combination";
import {TranslocoService} from "@jsverse/transloco";

@Pipe({
  name: 'filterCombination',
  pure: true,
  standalone: true
})
export class FilterCombinationPipe implements PipeTransform {
  private readonly translocoService = inject(TranslocoService);

  transform(value: FilterCombination): string {
    switch (value) {
      case FilterCombination.Or:
        return this.translocoService.translate('metadata-builder.or');
      case FilterCombination.And:
        return this.translocoService.translate('metadata-builder.and');
    }
  }

}
