import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'defaultDate',
  pure: true,
  standalone: true
})
export class DefaultDatePipe implements PipeTransform {

  transform(value: any, replacementString = 'Never'): string {
    if (value === null || value === undefined || value === '' || value === Infinity || Number.isNaN(value) || value === '1/1/01') return replacementString;
    return value;
  }

}
