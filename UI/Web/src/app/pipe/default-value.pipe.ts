import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'defaultValue'
})
export class DefaultValuePipe implements PipeTransform {

  transform(value: any, replacementString = '—'): string {
    if (value === null || value === undefined || value === '' || value === Infinity || value === NaN) return replacementString;
    return value;
  }

}
