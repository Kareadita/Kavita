import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'defaultDate'
})
export class DefaultDatePipe implements PipeTransform {

  transform(value: any, replacementString = 'Never'): string {
    if (value === null || value === undefined || value === '' || value === Infinity || value === NaN || value === '1/1/01') return replacementString;
    return value;
  }

}
