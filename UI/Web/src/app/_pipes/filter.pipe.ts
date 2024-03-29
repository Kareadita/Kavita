import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'filter',
  pure: false,
  standalone: true
})
export class FilterPipe implements PipeTransform {

  transform(items: any[], callback: (item: any) => boolean): any {
    if (!items || !callback) {
        return items;
    }
    const ret = items.filter(item => callback(item));
    if (ret.length === items.length) return items; // This will prevent a re-render
    return ret;
  }

}
