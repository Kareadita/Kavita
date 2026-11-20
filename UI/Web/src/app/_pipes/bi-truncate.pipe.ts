import { Pipe, PipeTransform } from '@angular/core';

/**
 * Truncate both on both sides. Length is the length on both sized. So total length after this pipe is at most 2*length + 3
 */
@Pipe({
  name: 'biTruncate',
})
export class BiTruncatePipe implements PipeTransform {
  transform(value: string, length: number): string {
    if (value.length < length * 2) {
      return value;
    }

    return value.slice(0, length) + '...' + value.slice(value.length - length);
  }
}
