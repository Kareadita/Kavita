import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'filter',
  pure: false,
  standalone: true
})
export class FilterPipe<T> implements PipeTransform {

  transform(items: T[], callback: (item: T) => boolean): T[] {
    if (!items || !callback) {
        return items;
    }
    const ret = items.filter(item => callback(item));
    if (ret.length === items.length) return items; // This will prevent a re-render
    return ret;
  }

}
