import {Pipe, PipeTransform} from '@angular/core';
import {HighlightColor} from "../book-reader/_models/annotations/annotation";

@Pipe({
  name: 'highlightColor'
})
export class HighlightColorPipe implements PipeTransform {

  transform(value: HighlightColor): string {
    switch (value) {
      case HighlightColor.Blue:
        return 'blue';
      case HighlightColor.Green:
        return 'green';
    }
  }

}
