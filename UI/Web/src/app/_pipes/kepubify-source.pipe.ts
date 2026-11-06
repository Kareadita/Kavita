import {Pipe, PipeTransform} from '@angular/core';
import {translate} from '@jsverse/transloco';
import {KepubifySource} from '../admin/_models/kepubify-source';

@Pipe({
  name: 'kepubifySource'
})
export class KepubifySourcePipe implements PipeTransform {

  transform(value: KepubifySource | null | undefined): string {
    switch (value) {
      case KepubifySource.Shipped:
        return translate('kepubify-source-pipe.shipped');
      case KepubifySource.Custom:
        return translate('kepubify-source-pipe.custom');
      default:
        return '';
    }
  }

}
