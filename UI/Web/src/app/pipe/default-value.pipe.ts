import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'defaultValue'
})
export class DefaultValuePipe implements PipeTransform {

  transform(value: any): string {
    if (value === null || value === undefined || value === '' || value === Infinity || value === NaN || value === {}) return '—';
    return value;
  }

}
