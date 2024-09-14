import { Pipe, PipeTransform } from '@angular/core';
import { FilterComparison } from 'src/app/_models/metadata/v2/filter-comparison';
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'filterComparison',
  standalone: true
})
export class FilterComparisonPipe implements PipeTransform {

  transform(value: FilterComparison): string {
    switch (value) {
      case FilterComparison.BeginsWith:
        return translate('filter-comparison-pipe.begins-with');
      case FilterComparison.Contains:
        return translate('filter-comparison-pipe.contains');
      case FilterComparison.Equal:
        return translate('filter-comparison-pipe.equal');
      case FilterComparison.GreaterThan:
        return translate('filter-comparison-pipe.greater-than');
      case FilterComparison.GreaterThanEqual:
        return translate('filter-comparison-pipe.greater-than-or-equal');
      case FilterComparison.LessThan:
        return translate('filter-comparison-pipe.less-than');
      case FilterComparison.LessThanEqual:
        return translate('filter-comparison-pipe.less-than-or-equal');
      case FilterComparison.Matches:
        return translate('filter-comparison-pipe.matches');
      case FilterComparison.NotContains:
        return translate('filter-comparison-pipe.does-not-contain');
      case FilterComparison.NotEqual:
        return translate('filter-comparison-pipe.not-equal');
      case FilterComparison.EndsWith:
        return translate('filter-comparison-pipe.ends-with');
      case FilterComparison.IsBefore:
        return translate('filter-comparison-pipe.is-before');
      case FilterComparison.IsAfter:
        return translate('filter-comparison-pipe.is-after');
      case FilterComparison.IsInLast:
        return translate('filter-comparison-pipe.is-in-last');
      case FilterComparison.IsNotInLast:
        return translate('filter-comparison-pipe.is-not-in-last');
      case FilterComparison.MustContains:
        return translate('filter-comparison-pipe.must-contains');
      case FilterComparison.IsEmpty:
        return translate('filter-comparison-pipe.is-empty');
      default:
        throw new Error(`Invalid FilterComparison value: ${value}`);
    }
  }

}
