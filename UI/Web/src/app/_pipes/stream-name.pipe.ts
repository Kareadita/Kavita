import { Pipe, PipeTransform } from '@angular/core';
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'streamName',
  standalone: true,
  pure: true
})
export class StreamNamePipe implements PipeTransform {

  transform(value: string): unknown {
    return translate('stream-pipe.' + value);
  }

}
