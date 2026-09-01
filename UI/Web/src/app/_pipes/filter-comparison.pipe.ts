import {inject, Pipe, PipeTransform} from '@angular/core';
import {FilterComparison} from "../_models/metadata/v2/filter-comparison";
import {TranslocoService} from "@jsverse/transloco";

@Pipe({
  name: 'filterComparison',
  standalone: true
})
export class FilterComparisonPipe implements PipeTransform {

  readonly translocoService = inject(TranslocoService);
  transform(value: FilterComparison): string {
    switch (value) {
      case FilterComparison.BeginsWith:
        return this.translocoService.translate('filter-comparison-pipe.begins-with');
      case FilterComparison.Contains:
        return this.translocoService.translate('filter-comparison-pipe.contains');
      case FilterComparison.Equal:
        return this.translocoService.translate('filter-comparison-pipe.equal');
      case FilterComparison.GreaterThan:
        return this.translocoService.translate('filter-comparison-pipe.greater-than');
      case FilterComparison.GreaterThanEqual:
        return this.translocoService.translate('filter-comparison-pipe.greater-than-or-equal');
      case FilterComparison.LessThan:
        return this.translocoService.translate('filter-comparison-pipe.less-than');
      case FilterComparison.LessThanEqual:
        return this.translocoService.translate('filter-comparison-pipe.less-than-or-equal');
      case FilterComparison.Matches:
        return this.translocoService.translate('filter-comparison-pipe.matches');
      case FilterComparison.NotContains:
        return this.translocoService.translate('filter-comparison-pipe.does-not-contain');
      case FilterComparison.NotEqual:
        return this.translocoService.translate('filter-comparison-pipe.not-equal');
      case FilterComparison.EndsWith:
        return this.translocoService.translate('filter-comparison-pipe.ends-with');
      case FilterComparison.IsBefore:
        return this.translocoService.translate('filter-comparison-pipe.is-before');
      case FilterComparison.IsAfter:
        return this.translocoService.translate('filter-comparison-pipe.is-after');
      case FilterComparison.IsInLast:
        return this.translocoService.translate('filter-comparison-pipe.is-in-last');
      case FilterComparison.IsNotInLast:
        return this.translocoService.translate('filter-comparison-pipe.is-not-in-last');
      case FilterComparison.MustContains:
        return this.translocoService.translate('filter-comparison-pipe.must-contains');
      case FilterComparison.IsEmpty:
        return this.translocoService.translate('filter-comparison-pipe.is-empty');
      case FilterComparison.IsNotEmpty:
        return this.translocoService.translate('filter-comparison-pipe.is-not-empty');
      default:
        throw new Error(`Invalid FilterComparison value: ${value}`);
    }
  }

}
