import { Pipe, PipeTransform } from '@angular/core';
import {TranslocoService} from "@jsverse/transloco";

@Pipe({
  name: 'defaultDate',
  pure: true,
  standalone: true
})
export class DefaultDatePipe implements PipeTransform {

  constructor(private translocoService: TranslocoService) {
  }
  transform(value: any, replacementString = 'default-date-pipe.never'): string {
    if (value === null || value === undefined || value === '' || value === Infinity || Number.isNaN(value) || value === '1/1/01') {
      return this.translocoService.translate(replacementString);
    };
    return value;
  }

}
