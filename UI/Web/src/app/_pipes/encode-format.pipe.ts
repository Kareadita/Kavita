import { Pipe, PipeTransform } from '@angular/core';
import {EncodeFormat} from "../admin/_models/encode-format";

@Pipe({
  name: 'encodeFormat',
  standalone: true
})
export class EncodeFormatPipe implements PipeTransform {

  transform(value: EncodeFormat): string {
    switch (value) {
      case EncodeFormat.PNG:
        return 'PNG';
      case EncodeFormat.WebP:
        return 'WebP';
      case EncodeFormat.AVIF:
        return 'AVIF';
    }
  }

}
