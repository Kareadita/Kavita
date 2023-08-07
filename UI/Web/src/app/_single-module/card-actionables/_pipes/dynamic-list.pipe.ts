import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'dynamicList',
  pure: true,
  standalone: true
})
export class DynamicListPipe implements PipeTransform {

  transform(value: any): Array<{title: string, data: any}> {
    if (value === undefined || value === null) return [];
    return value as {title: string, data: any}[];
  }

}
