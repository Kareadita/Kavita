import { Pipe, PipeTransform } from '@angular/core';
import { FilterComparison } from 'src/app/_models/metadata/v2/filter-comparison';

@Pipe({
  name: 'filterComparison'
})
export class FilterComparisonPipe implements PipeTransform {

  transform(value: FilterComparison): string {
    switch (value) {
      case FilterComparison.BeginsWith:
        return 'Begins with';
      case FilterComparison.Contains:
        return 'Contains';
      case FilterComparison.Equal:
        return 'Equal';
      case FilterComparison.GreaterThan:
        return 'Greater than';
      case FilterComparison.GreaterThanEqual:
        return 'Greater than or equal';
      case FilterComparison.LessThan:
        return 'Less than';
      case FilterComparison.LessThanEqual:
        return 'Less than or equal';
      case FilterComparison.Matches:
        return 'Matches';
      case FilterComparison.NotContains:
        return 'Does not contain';
      case FilterComparison.NotEqual:
        return 'Not equal';
      case FilterComparison.EndsWith:
        return 'Ends with';
      case FilterComparison.IsBefore:
        return 'Is before';
      case FilterComparison.IsAfter:
        return 'Is after';
      case FilterComparison.IsInLast:
        return 'Is in last';
      case FilterComparison.IsNotInLast:
        return 'Is not in last';
      default:
        throw new Error(`Invalid FilterComparison value: ${value}`);
    }
  }

}
