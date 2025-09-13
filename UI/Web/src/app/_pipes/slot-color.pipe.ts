import {Injectable, Pipe, PipeTransform} from '@angular/core';
import {RgbaColor} from "../book-reader/_models/annotations/highlight-slot";

@Pipe({
  name: 'slotColor'
})
@Injectable({ providedIn: 'root' })
export class SlotColorPipe implements PipeTransform {

  transform(value: RgbaColor) {
    return `rgba(${value.r}, ${value.g},${value.b}, ${value.a})`;
  }

}
