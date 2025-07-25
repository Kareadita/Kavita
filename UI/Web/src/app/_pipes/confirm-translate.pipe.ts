import { Pipe, PipeTransform } from '@angular/core';
import { translate } from '@jsverse/transloco';

@Pipe({
  name: 'confirmTranslate',
  standalone: true
})
export class ConfirmTranslatePipe implements PipeTransform {

  transform(value: string | undefined | null): string | undefined | null {
    if (!value) return value;

    if (value.startsWith('confirm.')) {
      return translate(value);
    }

    return value;
  }

}
